using System.Text;
using System.Text.Json;

namespace PlateformeFormation.Api.Services;

public class AiSuggestionService : IAiSuggestionService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    // Best available model on Groq free tier — 70B is far superior to 8B for classification
    // llama-3.3-70b-versatile was deprecated by Groq (June 2026); openai/gpt-oss-120b is Groq's
    // official migration recommendation.
    private const string Model = "openai/gpt-oss-120b";

    public AiSuggestionService(IHttpClientFactory factory, IConfiguration config)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured.");
    }

    public async Task<List<CategorySuggestion>> SuggestCategoriesAsync(IEnumerable<string> existingNames)
    {
        var existing = string.Join(", ", existingNames);

        var system = "Tu es un expert en gestion documentaire pour une plateforme de formation professionnelle. " +
                     "Tu réponds UNIQUEMENT avec du JSON brut, sans markdown ni explication.";

        var user = string.IsNullOrWhiteSpace(existing)
            ? "Propose 5 catégories pertinentes pour classer des documents de formation professionnelle (supports de cours, guides, évaluations, ressources pédagogiques). " +
              "Retourne un tableau JSON : [{\"nom\":\"...\",\"description\":\"...\"}]"
            : $"Catégories déjà existantes : {existing}.\n" +
              "Propose 5 NOUVELLES catégories complémentaires pour une plateforme de formation professionnelle. " +
              "Évite tout doublon ou chevauchement avec celles qui existent. " +
              "Retourne un tableau JSON : [{\"nom\":\"...\",\"description\":\"...\"}]";

        var text = await CallGroq(system, user, maxTokens: 600);
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start == -1 || end == -1) return [];

        return JsonSerializer.Deserialize<List<CategorySuggestion>>(
            text[start..(end + 1)],
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? [];
    }

    public async Task<DocumentCategoryResult?> SuggestCategoryForDocumentAsync(string extractedText, IEnumerable<CategoryMatch> categories)
    {
        var catList = categories.ToList();
        var snippet = extractedText.Length > 6000 ? extractedText[..6000] : extractedText;

        // Step 1: extract the precise topic of the document
        var topicSystem = "Tu es un expert en analyse de contenu. Tu réponds UNIQUEMENT avec du texte brut, sans JSON, sans markdown.";
        var topicUser =
            "Lis ce document et réponds en UNE SEULE PHRASE courte (max 10 mots) : de quel sujet précis parle ce document ?\n" +
            "IMPORTANT : base-toi UNIQUEMENT sur le contenu du document, pas sur son format.\n" +
            "Exemples de bonnes réponses : 'Développement mobile avec FlutterFlow', 'Comptabilité analytique', 'Sécurité réseau', 'Marketing digital'\n\n" +
            "Document :\n" + snippet;

        var topic = (await CallGroq(topicSystem, topicUser, maxTokens: 60)).Trim();

        // Step 2: pick or create category based on the extracted topic
        var catListText = catList.Count == 0
            ? "Aucune catégorie disponible."
            : string.Join("\n", catList.Select(c => $"  id={c.Id} | \"{c.Nom}\""));

        var classifySystem = "Tu réponds UNIQUEMENT avec un objet JSON brut. Aucun texte avant ou après. Aucun markdown.";
        var classifyUser =
            "Sujet du document : \"" + topic + "\"\n\n" +
            "Catégories disponibles :\n" + catListText + "\n\n" +
            "Règles STRICTES :\n" +
            "- Choisis la catégorie dont le nom correspond au SUJET du document.\n" +
            "- Si une catégorie existante correspond → {\"action\":\"pick\",\"id\":<id>,\"nom\":\"<nom>\"}\n" +
            "- Si aucune ne correspond → {\"action\":\"create\",\"nom\":\"<nom PRÉCIS basé sur le sujet, max 4 mots, en français>\",\"description\":\"<1 phrase>\"}\n" +
            "- Le nom créé doit refléter le sujet exact (ex: 'Développement Mobile', 'Comptabilité', 'Cybersécurité'), jamais des termes génériques comme 'Formation' ou 'Document'.\n" +
            "JSON uniquement :";

        var text = await CallGroq(classifySystem, classifyUser, maxTokens: 150);
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start == -1 || end == -1) return null;

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var action = doc.RootElement.GetProperty("action").GetString();

            if (action == "pick")
            {
                var id = doc.RootElement.GetProperty("id").GetInt32();
                var nom = doc.RootElement.GetProperty("nom").GetString() ?? "";
                return new DocumentCategoryResult(id, nom, false);
            }

            if (action == "create")
            {
                var nom = doc.RootElement.GetProperty("nom").GetString() ?? "Nouvelle catégorie";
                return new DocumentCategoryResult(0, nom, true);
            }
        }
        catch { /* malformed JSON — return null */ }

        return null;
    }

    private async Task<string> CallGroq(string systemPrompt, string userPrompt, int maxTokens = 512, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt }
            },
            max_tokens = maxTokens,
            temperature = 0.1,
            // gpt-oss models spend part of max_tokens on internal reasoning before writing content;
            // without this, a low budget can be exhausted mid-reasoning leaving an empty response.
            reasoning_effort = "low"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
