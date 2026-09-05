using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public class RetrievalService : IRetrievalService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmbeddingService _embedding;

    public RetrievalService(ApplicationDbContext db, IEmbeddingService embedding)
    {
        _db = db;
        _embedding = embedding;
    }

    public async Task<List<RetrievedSegment>> SearchAsync(
        string query, int topK = 5, int? documentId = null, int? categorieId = null,
        float minScore = 0.35f, IReadOnlyCollection<int>? allowedDocumentIds = null, CancellationToken ct = default)
    {
        var queryEmbedding = await _embedding.GetEmbeddingAsync(query, ct);

        var segmentsQuery = _db.DocumentSegments.AsNoTracking()
            .Include(s => s.Document)
            .Where(s => s.Document!.StatutTraitement == StatutTraitement.DISPONIBLE);

        if (documentId.HasValue)
            segmentsQuery = segmentsQuery.Where(s => s.DocumentId == documentId);
        if (categorieId.HasValue)
            segmentsQuery = segmentsQuery.Where(s => s.Document!.CategorieId == categorieId);
        // Ownership restriction, applied here (before Take(topK) below) rather than by the caller
        // filtering the returned list — filtering afterward can silently hand a non-admin fewer than
        // topK results (or none) even when they own plenty of relevant documents outside the top K.
        if (allowedDocumentIds is not null)
            segmentsQuery = segmentsQuery.Where(s => allowedDocumentIds.Contains(s.DocumentId));

        var segments = await segmentsQuery.ToListAsync(ct);

        return segments
            .Where(s => s.Embedding is not null)
            .Select(s => new RetrievedSegment(
                s.Id, s.DocumentId, s.Document!.Titre, s.Ordre, s.NumeroPage, s.ContenuTexte,
                CosineSimilarity(queryEmbedding, JsonSerializer.Deserialize<float[]>(s.Embedding!)!)
            ))
            // Below this score a chunk is essentially unrelated — feeding it to the model as
            // "context" invites answers grounded in noise instead of a clean "not in the docs".
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    internal static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new InvalidOperationException($"Embedding dimension mismatch ({a.Length} vs {b.Length}).");

        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0) return 0;
        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
