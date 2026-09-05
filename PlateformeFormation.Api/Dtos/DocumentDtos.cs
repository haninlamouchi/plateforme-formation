using System.ComponentModel.DataAnnotations;

namespace PlateformeFormation.Api.Dtos;

public record DocumentDto(
    int Id,
    string Titre,
    string? TypeDocument,
    int? CategorieId,
    string? CategorieNom,
    int UploadedBy,
    string UploaderNom,
    string? Resume,
    string? Langue,
    int? NombrePages,
    long? TailleFichier,
    string StatutTraitement,
    DateTime DateAjout,
    string FileUrl
);

public record UpdateDocumentRequest(
    [Required, MaxLength(255)] string Titre,
    int? CategorieId,
    [MaxLength(100)] string? TypeDocument,
    [MaxLength(50)] string? Langue
);
