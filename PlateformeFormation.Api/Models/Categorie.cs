namespace PlateformeFormation.Api.Models;

public class Categorie
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
}