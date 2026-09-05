using System.Text;
using System.Text.Json;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public class DocumentSummaryService : IDocumentSummaryService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ApplicationDbContext _db;

    // llama-3.3-70b-versatile was deprecated by Groq (June 2026); openai/gpt-oss-120b is Groq's
    // official migration recommendation.
    private const string Model = "openai/gpt-oss-120b";

    // Groq context budget — plenty for a summary, cheap enough not to worry about token cost.
    private const int MaxContentChars = 12000;

    private const string SystemPrompt =
        "Tu es un expert pédagogique qui résume des documents de formation pour des collègues pressés. " +
        "Rédige en français, de façon directe et concrète, sans ton marketing ni phrases creuses. " +
        "Structure ta réponse en Markdown : \n" +
        "1. Une phrase d'ouverture qui dit précisément de quoi parle le document et à qui il s'adresse.\n" +
        "2. Une liste à puces (lignes commençant par \"- \") de 3 à 6 informations concrètes et utiles " +
        "(objectifs, prérequis, public visé, durée, contenu abordé...), chacune en une phrase courte. " +
        "N'invente rien : si une information ne figure pas dans le document, ne la mentionne pas. " +
        "Ne mentionne pas les mots \"extrait\" ou \"contexte\".";

    public DocumentSummaryService(IHttpClientFactory factory, IConfiguration config, ApplicationDbContext db)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(45);
        _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured.");
        _db = db;
    }

    public async Task<string> GetOrGenerateSummaryAsync(
        Document document, List<DocumentSegment> segments, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(document.Resume))
            return document.Resume;

        var fullText = string.Join("\n\n", segments.OrderBy(s => s.Ordre).Select(s => s.ContenuTexte));
        if (fullText.Length > MaxContentChars)
            fullText = fullText[..MaxContentChars];

        var userPrompt = $"Titre du document : {document.Titre}\n\nContenu :\n{fullText}";
        var summary = await CallGroq(userPrompt, ct);

        document.Resume = summary;
        await _db.SaveChangesAsync(ct);

        return summary;
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
            max_tokens = 600,
            temperature = 0.2,
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
            throw new InvalidOperationException($"Groq summary request failed ({(int)response.StatusCode}): {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()?.Trim() ?? "";
    }
}
