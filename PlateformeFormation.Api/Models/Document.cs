namespace PlateformeFormation.Api.Models;

public class Document
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public string CheminFichier { get; set; } = string.Empty;
    public string? TypeDocument { get; set; }

    public int? CategorieId { get; set; }
    public Categorie? Categorie { get; set; }

    public int UploadedBy { get; set; }
    public Utilisateur? Uploader { get; set; }

    public string? Resume { get; set; }
    public string? Langue { get; set; }
    public int? NombrePages { get; set; }
    public long? TailleFichier { get; set; }
    public string? HashDocument { get; set; }

    public StatutTraitement StatutTraitement { get; set; } = StatutTraitement.EN_ATTENTE;
    public OrigineCategorie OrigineCategorie { get; set; } = OrigineCategorie.MANUELLE;
    public DateTime DateAjout { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentSegment> Segments { get; set; } = new List<DocumentSegment>();
    public ICollection<FormationDocument> FormationDocuments { get; set; } = new List<FormationDocument>();
}