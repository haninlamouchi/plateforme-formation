namespace PlateformeFormation.Api.Models;

public class Formation
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public string? Objectifs { get; set; }
    public decimal? DureeEstimee { get; set; }
    public string? Modules { get; set; }
    public string? Activites { get; set; }
    public string? MethodesEvaluation { get; set; }
    public StatutFormation Statut { get; set; } = StatutFormation.BROUILLON;

    public int CreePar { get; set; }
    public Utilisateur? Createur { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public ICollection<FormationDocument> FormationDocuments { get; set; } = new List<FormationDocument>();
    public ICollection<ExportHistorique> Exports { get; set; } = new List<ExportHistorique>();
}