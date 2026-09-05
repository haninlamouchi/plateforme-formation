namespace PlateformeFormation.Api.Models;

public class Notification
{
    public int Id { get; set; }

    public int UtilisateurId { get; set; }
    public Utilisateur? Utilisateur { get; set; }

    public string? Type { get; set; }
    public string? Contenu { get; set; }
    public string? Lien { get; set; }
    public bool Lue { get; set; } = false;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}