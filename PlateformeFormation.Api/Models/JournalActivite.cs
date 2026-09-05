namespace PlateformeFormation.Api.Models;

public class JournalActivite
{
    public int Id { get; set; }

    public int UtilisateurId { get; set; }
    public Utilisateur? Utilisateur { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? EntiteConcernee { get; set; }
    public int? EntiteId { get; set; }
    public DateTime DateAction { get; set; } = DateTime.UtcNow;
}