using System.Text;
using System.Text.Json;
using PlateformeFormation.Api.Models;
using static PlateformeFormation.Api.Helpers.FormationContentParser;
using static PlateformeFormation.Api.Helpers.FormationHarmonizer;

namespace PlateformeFormation.Api.Services;

// Closes the loop FormationQualityService opened: detecting problems is only half the value if the
// user still has to fix everything by hand. Two tiers, so a Groq call is never spent on arithmetic
// or structure a formula can already get right:
//   1. Deterministic — the same harmonizers used right after generation, reapplied to whatever the
//      formation currently looks like (arithmetic/references can drift again after a manual edit).
//   2. Targeted LLM — only when a content-judgment check is STILL failing after tier 1. The prompt
//      lists exactly the failing checks from the real report, not a generic rule set.
//
// Deliberately does NOT re-send the source documents: every remaining check (duplicate livrable,
// non-measurable objective, missing livrable chaining...) is a rewrite of content that already
// exists in the formation, not a request for new facts.
public class FormationCorrectionService : IFormationCorrectionService
{
    // Moved from NVIDIA to Groq — see FormationGenerationService.Model for why (NVIDIA account-level
    // provisioning failure, not fixable by picking a different model name).
    private const string Model = "openai/gpt-oss-120b";

    // Checks the deterministic tier already fully covers — never worth an LLM call on their own.
    // PROGRESSION_COMPETENCES joined this list once the numbered-module schema made forward
    // references mechanically strippable (SanitizeCompetencesPrerequises) instead of only detectable.
    private static readonly HashSet<string> DeterministicCodes =
        ["PONDERATION_100", "HEURES_MODULES", "ATTESTATION_NON_PONDEREE", "PROGRESSION_COMPETENCES"];

    private const string SystemPrompt =
        "Tu es ingénieur pédagogique senior en cabinet de conseil en formation professionnelle. On te donne un " +
        "programme de formation déjà rédigé (JSON) et une liste de PROBLÈMES PRÉCIS détectés automatiquement. " +
        "Corrige UNIQUEMENT les problèmes listés — ne touche à AUCUN autre champ, ne reformule rien qui n'est pas " +
        "concerné, ne renomme aucun module et ne change aucun \"numero\", n'invente aucun fait nouveau qui ne " +
        "serait pas déjà présent ailleurs dans le JSON fourni. Garde EXACTEMENT la même structure JSON et les " +
        "mêmes clés que le programme fourni (tu peux ajouter des éléments dans un tableau si un problème l'exige, " +
        "ex: une entrée d'évaluation continue manquante). N'ajoute JAMAIS d'attestation, de certificat ou de " +
        "certification dans \"evaluation\" — ce n'est jamais un élément noté. Chaque \"objectif\" de module doit " +
        "commencer par \"Être capable de\" ou \"Savoir\". Toute référence numérique (competencesPrerequises, " +
        "reutiliseLivrableModule) doit rester strictement inférieure au \"numero\" du module qui la porte. Réponds " +
        "UNIQUEMENT avec le JSON corrigé au format { \"objectifs\": {...}, \"modules\": [...], " +
        "\"methodesEvaluation\": [...] }, rien d'autre, sans markdown ni commentaire.";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly IFormationQualityService _quality;

    public FormationCorrectionService(IHttpClientFactory factory, IConfiguration config, IFormationQualityService quality)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(90);
        _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured.");
        _quality = quality;
    }

    public async Task<FormationDraft> CorrectAsync(Formation formation, CancellationToken ct = default)
    {
        // Nothing returned by this method is ever allowed to score lower than what the user started
        // with. Every candidate is scored and only the best-scoring one is returned.
        var originalScore = _quality.Evaluate(formation).Score;
        var originalDraft = new FormationDraft(
            formation.Titre, formation.Objectifs ?? "{}", formation.DureeEstimee,
            formation.Modules ?? "[]", formation.Activites ?? "[]", formation.MethodesEvaluation ?? "[]");

        // Tier 1 — deterministic, always applied. Arithmetic/references can drift again after a
        // manual edit, not just right after generation.
        var modulesJson = SanitizeCompetencesPrerequises(formation.Modules ?? "[]");
        var evaluationJson = RescaleEvaluationWeights(RemoveCertificationEntries(formation.MethodesEvaluation ?? "[]"));
        var harmonizedDuree = HarmonizeDureeFromModules(modulesJson, formation.Objectifs, formation.DureeEstimee);
        modulesJson = ApplyElisionToObjectifs(EnforceMeasurableObjectivePrefix(modulesJson));

        // activites_pedagogiques isn't targeted by any correction rule (R1-R13 don't cover it) — it
        // passes through unchanged rather than being blanked, since it's real user-facing content.
        var activitesJson = formation.Activites ?? "[]";
        var tier1Draft = new FormationDraft(formation.Titre, formation.Objectifs ?? "{}", harmonizedDuree, modulesJson, activitesJson, evaluationJson);
        var tier1Report = _quality.Evaluate(new Formation
        {
            Objectifs = formation.Objectifs, Modules = modulesJson,
            MethodesEvaluation = evaluationJson, DureeEstimee = harmonizedDuree,
        });

        var (bestDraft, bestScore) = tier1Report.Score >= originalScore
            ? (tier1Draft, tier1Report.Score)
            : (originalDraft, originalScore);

        // Tier 2 — only when a content-judgment check is still failing after the free tier.
        var remaining = tier1Report.Checks
            .Where(c => c.Statut == "ECHEC" && !DeterministicCodes.Contains(c.Code))
            .ToList();

        if (remaining.Count == 0) return bestDraft;

        try
        {
            var problemsText = string.Join("\n", remaining.Select(c => $"- {c.Libelle} : {c.Detail}"));
            var currentJson = $"{{\"objectifs\":{formation.Objectifs ?? "{}"},\"modules\":{modulesJson},\"methodesEvaluation\":{evaluationJson}}}";

            var userPrompt = $"Problèmes détectés à corriger (UNIQUEMENT ceux-ci) :\n{problemsText}\n\n" +
                              $"Programme actuel à corriger :\n{currentJson}";

            var responseText = await CallLlm(userPrompt, ct);
            var jsonText = ExtractJson(responseText);
            if (jsonText.Length == 0) return bestDraft;

            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var correctedModules = SanitizeCompetencesPrerequises(GetModulesJson(root));
            var correctedEvaluation = RescaleEvaluationWeights(RemoveCertificationEntries(GetEvaluationJson(root)));
            var correctedObjectifs = GetObjectifsJson(root);

            // Re-harmonize: the corrector may have reworded a livrable or added an evaluation entry
            // in a way that shifts the arithmetic again — the same safety net used after generation.
            var reHarmonizedDuree = HarmonizeDureeFromModules(correctedModules, correctedObjectifs, harmonizedDuree);
            correctedModules = ApplyElisionToObjectifs(EnforceMeasurableObjectivePrefix(correctedModules));

            var tier2Report = _quality.Evaluate(new Formation
            {
                Objectifs = correctedObjectifs, Modules = correctedModules,
                MethodesEvaluation = correctedEvaluation, DureeEstimee = reHarmonizedDuree,
            });

            return tier2Report.Score > bestScore
                ? new FormationDraft(formation.Titre, correctedObjectifs, reHarmonizedDuree, correctedModules, activitesJson, correctedEvaluation)
                : bestDraft;
        }
        catch
        {
            // Best-effort — an LLM failure on the targeted pass falls back to the best already-known
            // candidate rather than losing it.
            return bestDraft;
        }
    }

    private const int MaxTimeoutRetries = 1;

    // See FormationGenerationService.CallLlm for why: one retry on our own HttpClient.Timeout (not a
    // real caller cancellation) absorbs free-tier queueing flakiness without failing the whole
    // correction pass on infra load alone.
    private async Task<string> CallLlm(string userPrompt, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await CallLlmOnce(userPrompt, ct);
            }
            catch (TaskCanceledException) when (attempt < MaxTimeoutRetries && !ct.IsCancellationRequested)
            {
            }
        }
    }

    // Streamed for the same reason as FormationGenerationService.CallLlmOnce: a non-streaming request
    // sits idle for the full generation time, which is exactly the connection shape a NAT/proxy is
    // prone to silently kill mid-request.
    private async Task<string> CallLlmOnce(string userPrompt, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = 3800,
            temperature = 0.3,
            stream = true,
            reasoning_effort = "low"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Groq correction request failed ({(int)response.StatusCode}): {errorJson}");
        }

        var sb = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:")) continue;
            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0 || payload == "[DONE]") continue;

            // A malformed/truncated SSE chunk used to throw JsonException straight out of this method,
            // uncaught by CallLlm's retry loop (which only catches TaskCanceledException) — see
            // FormationGenerationService.CallLlmOnce for the same fix applied first. Skipping just this
            // chunk keeps the stream going.
            JsonDocument chunk;
            try { chunk = JsonDocument.Parse(payload); }
            catch (JsonException) { continue; }
            using (chunk)
            {
                var choices = chunk.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0) continue;

                if (choices[0].GetProperty("delta").TryGetProperty("content", out var contentEl)
                    && contentEl.ValueKind == JsonValueKind.String)
                {
                    sb.Append(contentEl.GetString());
                }
            }
        }

        return sb.ToString().Trim();
    }
}
