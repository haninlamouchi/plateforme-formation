using System.ComponentModel.DataAnnotations;

namespace PlateformeFormation.Api.Dtos;

public record CategorieDto(int Id, string Nom, string? Description, int DocumentCount);

public record CreateCategorieRequest(
    [Required, MaxLength(150)] string Nom,
    [MaxLength(1000)] string? Description
);

public record UpdateCategorieRequest(
    [Required, MaxLength(150)] string Nom,
    [MaxLength(1000)] string? Description
);
