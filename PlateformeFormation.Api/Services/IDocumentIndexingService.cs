namespace PlateformeFormation.Api.Services;

public interface IDocumentIndexingService
{
    Task<int> ChunkDocumentAsync(int documentId, CancellationToken ct = default);
}
