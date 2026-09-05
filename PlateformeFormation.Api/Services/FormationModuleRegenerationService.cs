using System.Text;
using System.Text.Json;
using PlateformeFormation.Api.Models;
using static PlateformeFormation.Api.Helpers.FormationContentParser;
using static PlateformeFormation.Api.Helpers.FormationHarmonizer;

namespace PlateformeFormation.Api.Services;

// Regenerates exactly one module — the "↺" action on the planning timeline. Unlike
// FormationCorrectionService (which deliberately withholds source documents because it only rewrites
// content that already exists), this asks for genuinely new material, so the formation's own source
// documents are re-sent, same as the initial generation. The neighbouring modules are sent too, purely
// as context, so the regenerated module still reads as part of the same sequence — they are never
// themselves modified, and "numero" is pinned defensively regardless of what the model returns.
public class FormationModuleRegenerationService : IFormationModuleRegenerationService
{
    // Moved from NVIDIA to Groq — see FormationGenerationService.Model for why (NVIDIA account-level
    // provisioning failure, not fixable by picking a different model name).
    private const string Model = "openai/gpt-oss-120b";
    private const int MaxContentCharsPerDoc = 5000;
    private const int MaxTimeoutRetries = 1;

    private const string SystemPrompt =
        "Tu es ingénieur pédagogique senior en cabinet de conseil en formation professionnelle. On te donne le " +
        "programme complet d'une formation (pour le contexte uniquement) et le contenu des documents source, " +
        "et on te demande de réécrire ENTIÈREMENT un seul module de ce programme, désigné par son \"numero\". " +
        "Le nouveau module doit rester cohérent avec les modules voisins (ne pas répéter leur contenu, respecter " +
        "la progression pédagogique), s'appuyer sur les documents source fournis, et respecter EXACTEMENT le " +
        "même schéma JSON qu'un module du programme. Ne change JAMAIS le \"numero\" — il doit rester identique à " +
        "celui demandé. Toute référence numérique (competencesPrerequises, reutilise_livrable_module) doit rester " +
        "strictement inférieure à ce \"numero\". L'objectif doit commencer par \"Être capable de\" ou \"Savoir\". " +
        "N'ajoute jamais d'attestation, de certificat ou de certification. Réponds UNIQUEMENT avec le JSON du " +
        "module au format { \"numero\": ..., \"titre\": ..., \"duree_heures\": ..., \"objectif\": ..., " +
        "\"methode\": {\"type\":..., \"pct_theorie\":..., \"pct_pratique\":...}, \"contenu\": [...], " +
        "\"exercice_formatif\": {\"type\":..., \"consigne\":..., \"criteres_reussite\":[...], \"materiel\":..., " +
        "\"duree_min\":...}, \"livrable\": ..., \"reutilise_livrable_module\": ..., " +
        "\"competences_prerequises\": [...], \"grille_evaluation\": [{\"critere\":..., \"pct\":...}], " +
        "\"notes_formateur\": [...] }, rien d'autre, sans markdown ni commentaire.";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public FormationModuleRegenerationService(IHttpClientFactory factory, IConfiguration config)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(90);
        _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured.");
    }

    public async Task<ModuleCard> RegenerateAsync(
        Formation formation, int numero, List<FormationSourceDocument> sources, CancellationToken ct = default)
    {
        var modules = ParseCards<ModuleCard>(formation.Modules, () => new ModuleCard(
            0, formation.Modules, null, null, null, null, null, null, null, null, null, null, null));
        var target = modules.FirstOrDefault(m => m.Numero == numero)
            ?? throw new KeyNotFoundException($"Module {numero} not found.");

        var neighbours = modules.Where(m => m.Numero != numero)
            .Select(m => $"- Module {m.Numero} : \"{m.Titre}\" — objectif : {m.Objectif} — livrable : {m.Livrable}");

        var sourcesText = string.Join("\n\n", sources.Select(s =>
            $"[Document : \"{s.Titre}\"]\n{Truncate(s.Content, MaxContentCharsPerDoc)}"));

        var currentModuleJson = JsonSerializer.Serialize(target, JsonWriteOpts);

        var userPrompt =
            $"Programme — modules voisins (contexte, ne pas modifier) :\n{string.Join("\n", neighbours)}\n\n" +
            $"Module à régénérer entièrement (numero {numero}), version actuelle pour référence :\n{currentModuleJson}\n\n" +
            $"Documents source :\n{sourcesText}";

        var responseText = await CallLlm(userPrompt, ct);
        var jsonText = ExtractJson(responseText);
        if (jsonText.Length == 0)
            throw new InvalidOperationException("Le service de régénération n'a pas renvoyé de JSON exploitable.");

        using var doc = JsonDocument.Parse(jsonText);
        var mapped = MapModule(doc.RootElement);
        var mappedJson = JsonSerializer.Serialize(mapped);

        // Reuse the same deterministic fixers applied right after generation — a single regenerated
        // module needs the same objective-prefix/elision/reference sanitation as a whole formation
        // does. They operate on a modules array, so the single module is wrapped for the call.
        var wrapped = ApplyElisionToObjectifs(EnforceMeasurableObjectivePrefix($"[{mappedJson}]"));
        wrapped = SanitizeCompetencesPrerequises(wrapped);

        var regenerated = JsonSerializer.Deserialize<List<ModuleCard>>(wrapped, JsonOpts)?.FirstOrDefault()
            ?? throw new InvalidOperationException("Le module régénéré est invalide.");

        // Never trust the model on the one field that must never move.
        return regenerated with { Numero = numero };
    }

    private static string Truncate(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..maxChars] + "...";

    // See FormationGenerationService.CallLlm for why: one retry on our own HttpClient.Timeout (not a
    // real caller cancellation) absorbs free-tier queueing flakiness without failing the whole
    // regeneration on infra load alone.
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
            max_tokens = 1800,
            temperature = 0.45,
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
            throw new InvalidOperationException($"Groq regeneration request failed ({(int)response.StatusCode}): {errorJson}");
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
