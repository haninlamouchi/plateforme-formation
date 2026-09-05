using System.ComponentModel.DataAnnotations;

namespace PlateformeFormation.Api.Dtos;

public record UserProfileDto(
    int Id,
    string Nom,
    string Email,
    string Role,
    string StatutCompte,
    bool Actif,
    DateTime DateCreation,
    DateTime? DateValidation,
    DateTime? DerniereConnexion,
    string? Discipline,
    string? Departement,
    string? Telephone,
    DateTime? DateNaissance,
    string? PhotoUrl
);

public record UpdateProfileRequest(
    [Required, MaxLength(150)] string Nom,
    [MaxLength(150)] string? Discipline,
    [MaxLength(150)] string? Departement,
    [MaxLength(30)] string? Telephone,
    DateTime? DateNaissance,
    [MaxLength(500)] string? PhotoUrl
);

public record ChangePasswordRequest(
    [Required] string OldPassword,
    [Required, MinLength(8), MaxLength(100)] string NewPassword
);
