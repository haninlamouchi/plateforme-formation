using System.ComponentModel.DataAnnotations;

namespace PlateformeFormation.Api.Dtos;

public record RegisterRequest(
    [Required, MaxLength(150)] string Nom,
    [Required, EmailAddress, MaxLength(190)] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required] string Role,
    [MaxLength(150)] string? Discipline = null,
    [MaxLength(150)] string? Departement = null,
    [MaxLength(30)] string? Telephone = null,
    DateTime? DateNaissance = null,
    [MaxLength(500)] string? PhotoUrl = null
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false
);

public record AuthResponse(string Token, string RefreshToken, int Id, string Nom, string Email, string Role, string? PhotoUrl);

public record RegisterResponse(string Message);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(8), MaxLength(100)] string NewPassword
);

public record RefreshRequest([Required] string RefreshToken);

public record GoogleLoginRequest([Required] string Credential);
