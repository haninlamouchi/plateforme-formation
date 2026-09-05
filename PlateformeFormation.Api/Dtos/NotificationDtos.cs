namespace PlateformeFormation.Api.Dtos;

public record NotificationDto(
    int Id, string? Type, string? Contenu, string? Lien, bool Lue, DateTime DateCreation
);
