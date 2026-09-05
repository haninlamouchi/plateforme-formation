namespace PlateformeFormation.Api.Dtos;

public record ChatUserDto(int Id, string Nom, string Email, string Role, string? PhotoUrl);

public record ChatMessageDto(
    int Id, int ExpediteurId, string ExpediteurNom, string? ExpediteurPhotoUrl,
    int? DestinataireId, string Contenu, DateTime DateEnvoi
);

public record ChatConversationDto(
    int UserId, string Nom, string? PhotoUrl,
    string? DernierMessage, DateTime? DateDernierMessage, int NonLus
);
