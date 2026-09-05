using System.Text;
using System.Text.Json;

namespace PlateformeFormation.Api.Services;

public class ChatbotService : IChatbotService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    // llama-3.3-70b-versatile was deprecated by Groq (June 2026); openai/gpt-oss-120b is Groq's
    // official migration recommendation.
    private const string Model = "openai/gpt-oss-120b";

    // Caps prompt size/cost — enough for short follow-ups ("et pour le jour 2 ?") without
    // dragging the whole conversation into every request.
    private const int MaxHistoryTurns = 6;
    private const int MaxContextCharsPerSegment = 3_500;

    private const string RetrievalQuerySystemPrompt =
        "You prepare semantic-search queries for a pedagogical-document library. " +
        "Use the conversation only to resolve pronouns, omissions, and references such as 'this document', " +
        "'the second module', or 'and after that'. Return a JSON array with one to three short, standalone French queries. " +
        "The first query must cover the complete question. For a comparison or a multi-part question, the following queries " +
        "must target its essential subtopics. Do not answer the question, add commentary, or invent information.";

    private const string GeneralKnowledgeSystemPrompt =
        "Tu es un assistant pedagogique generaliste. Reponds aux questions qui ne necessitent pas un document " +
        "importe dans la plateforme. Donne une reponse exacte, claire et pedagogique en francais, avec des exemples " +
        "si cela aide. Si la question est ambiguë, demande une precision. Ne presente jamais une supposition comme " +
        "un fait certain et signale brièvement les limites de ta connaissance. Ne dis pas que tu as consulte un PDF.";

    private const string SystemPrompt =
        "Tu es un assistant pédagogique qui répond aux questions en te basant UNIQUEMENT sur les extraits de documents " +
        "fournis en contexte ci-dessous. Si la réponse ne se trouve pas dans ces extraits, dis clairement que tu ne " +
        "disposes pas de cette information dans les documents, plutôt que d'inventer une réponse. Réponds en français, " +
        "de façon claire, naturelle et concise, comme si tu connaissais directement le sujet — ne mentionne pas les mots " +
        "\"extrait\", \"contexte\" ou \"document fourni\", et ne cite pas explicitement les sources dans le texte " +
        "(elles sont affichées séparément à l'utilisateur).";

    public ChatbotService(IHttpClientFactory factory, IConfiguration config)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured.");
    }

    public async Task<List<string>> BuildRetrievalQueriesAsync(
        string question, List<ChatTurn>? history = null, CancellationToken ct = default)
    {
        var recentHistory = history?.TakeLast(MaxHistoryTurns).ToList() ?? [];

        var messages = new List<object>
        {
            new { role = "system", content = RetrievalQuerySystemPrompt }
        };
        messages.AddRange(recentHistory.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = $"Question to rewrite: {question}" });

        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            messages,
            max_tokens = 400,
            temperature = 0,
            // gpt-oss models spend part of max_tokens on internal reasoning before ever writing
            // content; without this, a low max_tokens budget can be exhausted mid-reasoning and the
            // response comes back with empty content (looks like the model "doesn't know" anything,
            // even for a trivial prompt). "low" keeps that overhead small and predictable.
            reasoning_effort = "low"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return [question]; // A conversation enhancement must never make the chatbot unavailable.

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message")
            .GetProperty("content").GetString() ?? string.Empty;

        try
        {
            var arrayStart = content.IndexOf('[');
            var arrayEnd = content.LastIndexOf(']');
            if (arrayStart < 0 || arrayEnd <= arrayStart) return [question];

            var queries = JsonSerializer.Deserialize<List<string>>(content[arrayStart..(arrayEnd + 1)])
                ?.Select(q => q.Trim())
                .Where(q => q.Length is > 0 and <= 500)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            return queries is { Count: > 0 } ? queries : [question];
        }
        catch (JsonException)
        {
            return [question];
        }
    }

    public async Task<ChatAnswer> AskAsync(
        string question, List<RetrievedSegment> context, List<ChatTurn>? history = null,
        CancellationToken ct = default)
    {
        if (context.Count == 0)
        {
            return new ChatAnswer(
                "Je n'ai trouvé aucun document pertinent pour répondre à cette question.",
                context);
        }

        var contextText = string.Join("\n\n", context.Select(c =>
            $"[Source : \"{c.DocumentTitre}\", extrait {c.Ordre + 1}]\n{TrimForContext(c.ContenuTexte)}"));

        var userPrompt = $"Contexte :\n{contextText}\n\nQuestion : {question}";

        var recentHistory = history?.TakeLast(MaxHistoryTurns) ?? [];
        var answer = await CallGroq(SystemPrompt, recentHistory, userPrompt, 1_000, ct);
        return new ChatAnswer(answer, context);
    }

    public async Task<string> AskGeneralAsync(
        string question, List<ChatTurn>? history = null, CancellationToken ct = default)
    {
        var recentHistory = history?.TakeLast(MaxHistoryTurns) ?? [];
        var userPrompt = $"Question generale : {question}";
        return await CallGroq(GeneralKnowledgeSystemPrompt, recentHistory, userPrompt, 1_000, ct);
    }

    private static string TrimForContext(string text) =>
        text.Length <= MaxContextCharsPerSegment ? text : text[..MaxContextCharsPerSegment] + "...";

    private async Task<string> CallGroq(
        string systemPrompt, IEnumerable<ChatTurn> history, string userPrompt, int maxTokens, CancellationToken ct)
    {
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = systemPrompt +
                    " For a complex question, identify every requested part and answer each one. " +
                    "You may combine information from several passages, but distinguish an explicit fact from a reasonable " +
                    "pedagogical inference. Prefer a short structured answer when it improves clarity."
            }
        };
        messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = userPrompt });

        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            messages,
            max_tokens = maxTokens,
            temperature = 0.2,
            // See BuildRetrievalQueriesAsync for why: gpt-oss models can exhaust max_tokens on
            // internal reasoning before writing any content, especially for prompts that "look"
            // simple to a human but the model still deliberates over — "low" bounds that overhead.
            reasoning_effort = "low"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Groq chat request failed ({(int)response.StatusCode}): {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
