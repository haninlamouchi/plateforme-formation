namespace PlateformeFormation.Api.Models;

public class ExportHistorique
{
    public int Id { get; set; }

    public int FormationId { get; set; }
    public Formation? Formation { get; set; }

    public int UtilisateurId { get; set; }
    public Utilisateur? Utilisateur { get; set; }

    public FormatExport Format { get; set; }
    public string? CheminFichier { get; set; }
    public DateTime DateExport { get; set; } = DateTime.UtcNow;
}