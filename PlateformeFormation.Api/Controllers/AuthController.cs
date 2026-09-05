using System.Security.Cryptography;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly TokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditService _audit;

    // A fixed bcrypt hash verified against when the email doesn't exist (or is a Google-only account),
    // so Login always pays bcrypt's cost regardless of whether the account exists — otherwise an
    // unknown-email request returns near-instantly while a known one takes bcrypt's ~100ms, letting an
    // attacker enumerate registered emails purely from response time despite the generic error message.
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("timing-attack-mitigation");

    public AuthController(
        ApplicationDbContext db, TokenService tokenService,
        IEmailService emailService, IConfiguration config,
        ILogger<AuthController> logger, IAuditService audit)
    {
        _db = db; _tokenService = tokenService;
        _emailService = emailService; _config = config;
        _logger = logger; _audit = audit;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request)
    {
        var (pwValid, pwError) = PasswordHelper.Validate(request.Password);
        if (!pwValid) return BadRequest(new { message = pwError });

        if (await _db.Utilisateurs.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "This email is already in use." });

        if (!Enum.TryParse<RoleUtilisateur>(request.Role, out var role) ||
            role == RoleUtilisateur.ADMINISTRATEUR)
            return BadRequest(new { message = "Invalid role." });

        var utilisateur = new Utilisateur
        {
            Nom = request.Nom, Email = request.Email,
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role, StatutCompte = StatutCompte.EN_ATTENTE,
            Discipline = request.Discipline, Departement = request.Departement,
            Telephone = request.Telephone, DateNaissance = request.DateNaissance,
            PhotoUrl = request.PhotoUrl
        };

        _db.Utilisateurs.Add(utilisateur);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created,
            new RegisterResponse("Your request has been sent. You will receive an email once approved."));
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var utilisateur = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Always verify password (even for unknown email or a Google-only account) to prevent timing
        // attacks — verifying against DummyPasswordHash in those cases keeps the bcrypt cost identical
        // to the real-account path, so response time can't be used to enumerate registered emails.
        bool passwordMatches = false;
        if (utilisateur is not null && !utilisateur.IsGoogleUser)
        {
            // Check lockout before expensive bcrypt
            if (utilisateur.LockoutUntil.HasValue && utilisateur.LockoutUntil > DateTime.UtcNow)
            {
                var remaining = (int)(utilisateur.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
                return StatusCode(429, new { message = $"Account locked due to too many failed attempts. Try again in {remaining} minute(s)." });
            }

            try { passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, utilisateur.MotDePasseHash); }
            catch (BCrypt.Net.SaltParseException) { }
        }
        else
        {
            try { BCrypt.Net.BCrypt.Verify(request.Password, DummyPasswordHash); }
            catch (BCrypt.Net.SaltParseException) { }
        }

        if (utilisateur is null || !passwordMatches)
        {
            if (utilisateur is not null && !utilisateur.IsGoogleUser)
            {
                utilisateur.FailedLoginAttempts++;
                if (utilisateur.FailedLoginAttempts >= 5)
                {
                    utilisateur.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                    utilisateur.FailedLoginAttempts = 0;
                    await _db.SaveChangesAsync();
                    return StatusCode(429, new { message = "Too many failed attempts. Account locked for 15 minutes." });
                }
                await _db.SaveChangesAsync();
            }
            return Unauthorized(new { message = "Incorrect email or password." });
        }

        if (utilisateur.StatutCompte == StatutCompte.EN_ATTENTE)
            return StatusCode(403, new { message = "Your account is pending approval." });
        if (utilisateur.StatutCompte == StatutCompte.REFUSE)
            return StatusCode(403, new { message = "Your account has been refused." });
        if (utilisateur.StatutCompte != StatutCompte.VALIDE)
            return StatusCode(403, new { message = "Your account is not active." });
        if (!utilisateur.Actif)
            return Unauthorized(new { message = "This account is disabled." });

        // Reset failed attempts on success
        utilisateur.FailedLoginAttempts = 0;
        utilisateur.LockoutUntil = null;
        utilisateur.DerniereConnexion = DateTime.UtcNow;

        var expiry = request.RememberMe ? TimeSpan.FromDays(30) : (TimeSpan?)null;
        var token = _tokenService.GenerateToken(utilisateur, expiry);
        var (refreshToken, refreshExpiry) = GenerateRefreshToken();
        utilisateur.RefreshToken = refreshToken;
        utilisateur.RefreshTokenExpiry = refreshExpiry;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(utilisateur.Id, "LOGIN");
        return Ok(new AuthResponse(token, refreshToken, utilisateur.Id, utilisateur.Nom, utilisateur.Email, utilisateur.Role.ToString(), utilisateur.PhotoUrl));
    }

    [HttpPost("google")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _config["Google:ClientId"] }
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid Google token received.");
            return Unauthorized(new { message = "Invalid Google token." });
        }

        var utilisateur = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Email == payload.Email);

        if (utilisateur is null)
        {
            utilisateur = new Utilisateur
            {
                Nom = payload.Name ?? payload.Email,
                Email = payload.Email,
                MotDePasseHash = "",
                Role = RoleUtilisateur.RESPONSABLE_PEDAGOGIQUE,
                StatutCompte = StatutCompte.EN_ATTENTE,
                IsGoogleUser = true,
                Actif = true,
                PhotoUrl = payload.Picture,
                DateCreation = DateTime.UtcNow
            };
            _db.Utilisateurs.Add(utilisateur);
            await _db.SaveChangesAsync();
            return StatusCode(403, new { message = "Your Google account has been registered and is pending admin approval." });
        }

        if (utilisateur.StatutCompte != StatutCompte.VALIDE)
            return StatusCode(403, new { message = "Your account is not active yet." });
        if (!utilisateur.Actif)
            return Unauthorized(new { message = "This account is disabled." });

        utilisateur.DerniereConnexion = DateTime.UtcNow;
        var token = _tokenService.GenerateToken(utilisateur);
        var (refreshToken, refreshExpiry) = GenerateRefreshToken();
        utilisateur.RefreshToken = refreshToken;
        utilisateur.RefreshTokenExpiry = refreshExpiry;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(utilisateur.Id, "LOGIN_GOOGLE");
        return Ok(new AuthResponse(token, refreshToken, utilisateur.Id, utilisateur.Nom, utilisateur.Email, utilisateur.Role.ToString(), utilisateur.PhotoUrl));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var utilisateur = await _db.Utilisateurs.FirstOrDefaultAsync(u =>
            u.RefreshToken == request.RefreshToken &&
            u.RefreshTokenExpiry != null &&
            u.RefreshTokenExpiry > DateTime.UtcNow);

        if (utilisateur is null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        if (!utilisateur.Actif || utilisateur.StatutCompte != StatutCompte.VALIDE)
            return Unauthorized(new { message = "Account is not active." });

        var token = _tokenService.GenerateToken(utilisateur);
        var (refreshToken, refreshExpiry) = GenerateRefreshToken();
        utilisateur.RefreshToken = refreshToken;
        utilisateur.RefreshTokenExpiry = refreshExpiry;
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse(token, refreshToken, utilisateur.Id, utilisateur.Nom, utilisateur.Email, utilisateur.Role.ToString(), utilisateur.PhotoUrl));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var generic = new { message = "If an account exists with this email, a reset link has been sent." };
        var utilisateur = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (utilisateur is null || utilisateur.StatutCompte != StatutCompte.VALIDE)
            return Ok(generic);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

        utilisateur.ResetPasswordToken = token;
        utilisateur.ResetPasswordTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        try
        {
            var frontendUrl = _config["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
            await _emailService.SendPasswordResetAsync(utilisateur, $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(token)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reset email to {Email}.", utilisateur.Email);
        }

        return Ok(generic);
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var (pwValid, pwError) = PasswordHelper.Validate(request.NewPassword);
        if (!pwValid) return BadRequest(new { message = pwError });

        var utilisateur = await _db.Utilisateurs.FirstOrDefaultAsync(u =>
            u.ResetPasswordToken == request.Token &&
            u.ResetPasswordTokenExpiry != null &&
            u.ResetPasswordTokenExpiry > DateTime.UtcNow);

        if (utilisateur is null)
            return BadRequest(new { message = "This reset link is invalid or has expired." });

        utilisateur.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        utilisateur.ResetPasswordToken = null;
        utilisateur.ResetPasswordTokenExpiry = null;
        utilisateur.RefreshToken = null;
        utilisateur.RefreshTokenExpiry = null;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Your password has been reset successfully." });
    }

    private static (string Token, DateTime Expiry) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return (Convert.ToBase64String(bytes), DateTime.UtcNow.AddDays(7));
    }
}
