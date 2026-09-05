using System.Text.Json;
using PlateformeFormation.Api.Models;
using static PlateformeFormation.Api.Helpers.FormationContentParser;

namespace PlateformeFormation.Api.Services;

// Reuses the embeddings already computed for chatbot/formation-generation retrieval — no extra LLM
// call. For each module, a single semantic search (title + sous-thèmes as the query) restricted
// in-memory to the formation's own source documents finds the passage(s) that actually back it,
// turning the RAG pipeline from invisible plumbing into something the user can audit — did this
// module come from a real passage, or is there nothing behind it?
public class FormationTraceabilityService : IFormationTraceabilityService
{
    private const int TopKPerModule = 20;
    private const int MaxSourcesPerModule = 2;
    private const int ExtraitMaxChars = 300;

    private readonly IRetrievalService _retrieval;

    public FormationTraceabilityService(IRetrievalService retrieval)
    {
        _retrieval = retrieval;
    }

    public async Task<string> AttachSourcesAsync(string modulesJson, List<int> documentIds, CancellationToken ct = default)
    {
        var modules = ParseCards<ModuleCard>(modulesJson, () => new ModuleCard(
            0, modulesJson, null, null, null, null, null, null, null, null, null, null, null));
        if (modules.Count == 0 || documentIds.Count == 0) return modulesJson;

        var docIdSet = documentIds.ToHashSet();
        var updated = new List<ModuleCard>(modules.Count);

        foreach (var module in modules)
        {
            var query = BuildQuery(module);
            if (query.Length == 0) { updated.Add(module); continue; }

            List<RetrievedSegment> results;
            try
            {
                // A single call per module — restricting to documentIds happens in memory afterwards
                // rather than looping SearchAsync per source document, which would re-embed the same
                // query once per document for no benefit (the formation's sources were already
                // access-checked at generation time, so post-hoc filtering here is safe).
                results = await _retrieval.SearchAsync(query, topK: TopKPerModule, minScore: 0f, ct: ct);
            }
            catch
            {
                // Best-effort — a retrieval failure for one module shouldn't lose the whole formation.
                updated.Add(module);
                continue;
            }

            var sources = results
                .Where(r => docIdSet.Contains(r.DocumentId))
                .OrderByDescending(r => r.Score)
                .Take(MaxSourcesPerModule)
                .Select(r => new ModuleSource(r.DocumentId, r.DocumentTitre, r.Ordre, Truncate(r.ContenuTexte), r.Score))
                .ToList();

            updated.Add(module with { Sources = sources.Count > 0 ? sources : null });
        }

        return JsonSerializer.Serialize(updated, JsonWriteOpts);
    }

    private static string BuildQuery(ModuleCard module)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(module.Titre)) parts.Add(module.Titre);
        if (module.Contenu is { Count: > 0 }) parts.AddRange(module.Contenu);
        return string.Join(" ", parts);
    }

    private static string Truncate(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= ExtraitMaxChars ? trimmed : trimmed[..ExtraitMaxChars].TrimEnd() + "…";
    }
}
