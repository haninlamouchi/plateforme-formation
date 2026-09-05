using System.Text;
using System.Text.Json;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

// Purely conversational: extracts competences for the chatbot's answer, nothing is persisted
// (no dedicated Competence table — that was tried and rolled back earlier).
public class DocumentCompetenceService : IDocumentCompetenceService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    // llama-3.3-70b-versatile was deprecated by Groq (June 2026); openai/gpt-oss-120b is Groq's
    // official migration recommendation.
    private const string Model = "openai/gpt-oss-120b";
    private const int MaxContentChars = 12000;

    private const string SystemPrompt =
        "Tu es un assistant pédagogique. Identifie jusqu'à 8 compétences ou connaissances clés enseignées " +
        "dans le document fourni. Réponds UNIQUEMENT avec un tableau JSON brut, sans markdown ni texte autour, " +
        "au format : [{\"libelle\":\"...\",\"domaine\":\"...\"}]. \"libelle\" = la compétence en quelques mots, " +
        "\"domaine\" = la catégorie/thème court auquel elle appartient (ex: \"Programmation\", \"Sécurité\", " +
        "\"Gestion de projet\"). Réponds en français.";

    public DocumentCompetenceService(IHttpClientFactory factory, IConfiguration config)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(45);
        _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured.");
    }

    public async Task<List<CompetenceItem>> ExtractCompetencesAsync(
        Document document, List<DocumentSegment> segments, CancellationToken ct = default)
    {
        var fullText = string.Join("\n\n", segments.OrderBy(s => s.Ordre).Select(s => s.ContenuTexte));
        if (fullText.Length > MaxContentChars)
            fullText = fullText[..MaxContentChars];

        var userPrompt = $"Titre du document : {document.Titre}\n\nContenu :\n{fullText}";
        var text = await CallGroq(userPrompt, ct);

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start == -1 || end == -1) return [];

        try
        {
            return JsonSerializer.Deserialize<List<CompetenceItem>>(
                text[start..(end + 1)],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string> CallGroq(string userPrompt, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = 500,
            temperature = 0.1,
            // gpt-oss models spend part of max_tokens on internal reasoning before writing content;
            // without this, a low budget can be exhausted mid-reasoning leaving an empty response.
            reasoning_effort = "low"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Groq competences request failed ({(int)response.StatusCode}): {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()?.Trim() ?? "";
    }
}
