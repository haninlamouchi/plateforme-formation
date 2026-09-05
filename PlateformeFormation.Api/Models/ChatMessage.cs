namespace PlateformeFormation.Api.Models;

public class ChatMessage
{
    public int Id { get; set; }

    public int ExpediteurId { get; set; }
    public Utilisateur? Expediteur { get; set; }

    // NULL => message envoyé au salon général ; sinon => message privé
    public int? DestinataireId { get; set; }
    public Utilisateur? Destinataire { get; set; }

    public string Contenu { get; set; } = string.Empty;
    public DateTime DateEnvoi { get; set; } = DateTime.UtcNow;
    public bool Lu { get; set; } = false;
}
