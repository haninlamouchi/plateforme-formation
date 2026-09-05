namespace PlateformeFormation.Api.Models;

public class FormationDocument
{
    public int FormationId { get; set; }
    public Formation? Formation { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public decimal? ScorePertinence { get; set; }
}