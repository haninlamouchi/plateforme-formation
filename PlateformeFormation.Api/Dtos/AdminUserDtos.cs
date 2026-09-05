using System.ComponentModel.DataAnnotations;

namespace PlateformeFormation.Api.Dtos;

public record PendingUserDto(
    int Id, string Nom, string Email, string Role, string StatutCompte,
    DateTime DateCreation, string? Discipline, string? Departement, string? Telephone
);

public record UserListItemDto(
    int Id, string Nom, string Email, string Role, string StatutCompte,
    bool Actif, DateTime DateCreation, DateTime? DateValidation,
    DateTime? DerniereConnexion, string? Discipline, string? Departement,
    string? Telephone, string? PhotoUrl
);

public record UpdateUserRequest(
    [Required, MaxLength(150)] string Nom,
    [Required] string Role,
    bool Actif
);

public record AdminStatsDto(
    int ActiveUsers,
    int PendingAccounts,
    int TotalDocuments,
    int TotalFormations,
    int TotalCategories,
    int RecentUploads
);

public record AuditLogEntryDto(
    int Id, string Action, string? EntiteConcernee, int? EntiteId,
    DateTime DateAction, int UtilisateurId, string UtilisateurNom, string UtilisateurEmail
);

public record CategoryStatDto(string Name, int Count);
public record RoleStatDto(string Role, int Count);
public record MonthlyStatDto(string Month, int Count);
public record AdminChartsDto(
    IEnumerable<CategoryStatDto> DocsByCategory,
    IEnumerable<RoleStatDto> UsersByRole,
    IEnumerable<MonthlyStatDto> UploadsByMonth
);
