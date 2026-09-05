namespace PlateformeFormation.Api.Models;

public class AlerteDoublon
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int DocumentSimilaireId { get; set; }
    public Document? DocumentSimilaire { get; set; }

    public decimal? ScoreSimilarite { get; set; }
    public DecisionDoublon Decision { get; set; } = DecisionDoublon.EN_ATTENTE;
    public DateTime DateDetection { get; set; } = DateTime.UtcNow;
}