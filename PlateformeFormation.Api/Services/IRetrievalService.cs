namespace PlateformeFormation.Api.Services;

public record RetrievedSegment(
    int SegmentId,
    int DocumentId,
    string DocumentTitre,
    int Ordre,
    int? NumeroPage,
    string ContenuTexte,
    float Score
);

public interface IRetrievalService
{
    // `allowedDocumentIds`, when not null, restricts the search to that set of documents BEFORE
    // scoring/ranking/Take(topK) — a non-admin caller's ownership filter must go here, not applied to
    // the already-topK'd results afterward, or a non-admin can silently get fewer than topK results
    // (or none) even when they own plenty of relevant documents outside the global top K.
    Task<List<RetrievedSegment>> SearchAsync(
        string query, int topK = 5, int? documentId = null, int? categorieId = null,
        float minScore = 0.35f, IReadOnlyCollection<int>? allowedDocumentIds = null, CancellationToken ct = default);
}
