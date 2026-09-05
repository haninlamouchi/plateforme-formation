using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ProfileController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db; _audit = audit;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetMe()
    {
        var id = GetCurrentUserId();
        var user = await _db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        return Ok(new UserProfileDto(
            user.Id, user.Nom, user.Email, user.Role.ToString(),
            user.StatutCompte.ToString(), user.Actif,
            user.DateCreation, user.DateValidation, user.DerniereConnexion,
            user.Discipline, user.Departement, user.Telephone,
            user.DateNaissance, user.PhotoUrl
        ));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        // UploadController's avatar endpoint only ever returns "/uploads/avatars/{fileName}" — anything
        // else (an absolute URL, a path outside that folder) isn't a photo this app actually hosts, and
        // this field is later rendered as an <img src> by the frontend with no further validation.
        if (!string.IsNullOrWhiteSpace(request.PhotoUrl) && !request.PhotoUrl.StartsWith("/uploads/avatars/"))
            return BadRequest(new { message = "Invalid photo URL." });

        var id = GetCurrentUserId();
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        user.Nom = request.Nom;
        user.Discipline = request.Discipline;
        user.Departement = request.Departement;
        user.Telephone = request.Telephone;
        user.DateNaissance = request.DateNaissance;
        user.PhotoUrl = request.PhotoUrl;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(id, "UPDATE_PROFILE", "utilisateur", id);
        return Ok(new { message = "Profile updated successfully." });
    }

    [HttpPut("me/change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var id = GetCurrentUserId();
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (user.IsGoogleUser)
            return BadRequest(new { message = "Google accounts cannot change password here." });

        var (pwValid, pwError) = PasswordHelper.Validate(request.NewPassword);
        if (!pwValid) return BadRequest(new { message = pwError });

        bool valid = false;
        try { valid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.MotDePasseHash); }
        catch (BCrypt.Net.SaltParseException) { }

        if (!valid)
            return BadRequest(new { message = "Current password is incorrect." });

        user.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(id, "CHANGE_PASSWORD", "utilisateur", id);
        return Ok(new { message = "Password changed successfully." });
    }
}
