using System.Text;
using System.Text.Json;

namespace PlateformeFormation.Api.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    // hf-inference only actively hosts a subset of embedding models — this one is
    // multilingual (fits our French/English PDFs) and confirmed available on that provider.
    private const string Model = "microsoft/harrier-oss-v1-0.6b";
    private const int MaxTimeoutRetries = 2;

    public EmbeddingService(IHttpClientFactory factory, IConfiguration config)
    {
        _http = factory.CreateClient();
        // Free-tier hf-inference can cold-start a model well past a short timeout even with
        // wait_for_model — 60s (plus the retry below) gives a cold start room without hanging forever.
        _http.Timeout = TimeSpan.FromSeconds(60);
        _apiKey = config["HuggingFace:ApiKey"] ?? throw new InvalidOperationException("HuggingFace:ApiKey not configured.");
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await GetEmbeddingOnceAsync(text, ct);
            }
            // A NAT/proxy silently killing a slow connection surfaces as our own HttpClient.Timeout
            // firing mid-read, indistinguishable from a real failure without this guard — one retry
            // absorbs that instead of failing the whole chatbot/indexing request on infra flakiness
            // alone. Same pattern already used by FormationGenerationService/FormationCorrectionService
            // for their NVIDIA calls; this file just never had it.
            catch (TaskCanceledException) when (attempt < MaxTimeoutRetries && !ct.IsCancellationRequested)
            {
            }
        }
    }

    private async Task<float[]> GetEmbeddingOnceAsync(string text, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            inputs = text,
            options = new { wait_for_model = true }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://router.huggingface.co/hf-inference/models/{Model}");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HuggingFace embedding request failed ({(int)response.StatusCode}): {json}");

        using var doc = JsonDocument.Parse(json);
        return ParseEmbedding(doc.RootElement);
    }

    // HuggingFace returns either a flat array of floats (one sentence embedding)
    // or a nested array (one embedding per input) — we always send a single input,
    // so a nested response means we take its first (and only) element.
    private static float[] ParseEmbedding(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            throw new InvalidOperationException("Unexpected embedding response shape.");

        var first = root[0];
        var vector = first.ValueKind == JsonValueKind.Array ? first : root;
        return vector.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }
}
