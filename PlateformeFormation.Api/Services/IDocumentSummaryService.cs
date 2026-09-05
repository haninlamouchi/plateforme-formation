using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public interface IDocumentSummaryService
{
    Task<string> GetOrGenerateSummaryAsync(
        Document document, List<DocumentSegment> segments, CancellationToken ct = default);
}
