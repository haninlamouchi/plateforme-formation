using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public class DocumentIndexingService : IDocumentIndexingService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IChunkingService _chunking;
    private readonly IEmbeddingService _embedding;

    public DocumentIndexingService(ApplicationDbContext db, IWebHostEnvironment env, IChunkingService chunking, IEmbeddingService embedding)
    {
        _db = db; _env = env; _chunking = chunking; _embedding = embedding;
    }

    public async Task<int> ChunkDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new InvalidOperationException($"Document {documentId} not found.");

        doc.StatutTraitement = StatutTraitement.EN_COURS;
        await _db.SaveChangesAsync(ct);

        try
        {
            var fullPath = Path.Combine(_env.WebRootPath,
                doc.CheminFichier.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            List<PdfTextExtractor.ExtractedPdfPage> pages;
            await using (var stream = File.OpenRead(fullPath))
                pages = PdfTextExtractor.ExtractPages(stream);

            var chunks = pages.SelectMany(page => _chunking.SplitIntoChunks(page.Text)
                .Select(chunk => (Text: chunk, PageNumber: page.Number)))
                .ToList();

            var existingSegments = await _db.DocumentSegments
                .Where(s => s.DocumentId == documentId)
                .ToListAsync(ct);
            _db.DocumentSegments.RemoveRange(existingSegments);

            for (var i = 0; i < chunks.Count; i++)
            {
                var embedding = await _embedding.GetEmbeddingAsync(chunks[i].Text, ct);
                _db.DocumentSegments.Add(new DocumentSegment
                {
                    DocumentId = documentId,
                    ContenuTexte = chunks[i].Text,
                    Ordre = i,
                    NumeroPage = chunks[i].PageNumber,
                    Embedding = JsonSerializer.Serialize(embedding),
                });
            }

            doc.StatutTraitement = StatutTraitement.DISPONIBLE;
            await _db.SaveChangesAsync(ct);
            return chunks.Count;
        }
        catch
        {
            doc.StatutTraitement = StatutTraitement.ERREUR;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }
}
