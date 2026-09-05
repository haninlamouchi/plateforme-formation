using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public record CompetenceItem(string Libelle, string Domaine);

public interface IDocumentCompetenceService
{
    Task<List<CompetenceItem>> ExtractCompetencesAsync(
        Document document, List<DocumentSegment> segments, CancellationToken ct = default);
}
