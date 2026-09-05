namespace PlateformeFormation.Api.Models;

public class DocumentSegment
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public string ContenuTexte { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public int? NumeroPage { get; set; }
    public string? Embedding { get; set; }
}
