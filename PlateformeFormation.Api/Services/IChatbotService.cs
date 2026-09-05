namespace PlateformeFormation.Api.Services;

public record ChatAnswer(string Answer, List<RetrievedSegment> Sources);

// Role must be "user" or "assistant" (mirrors Groq/OpenAI chat message roles).
public record ChatTurn(string Role, string Content);

public interface IChatbotService
{
    Task<List<string>> BuildRetrievalQueriesAsync(
        string question, List<ChatTurn>? history = null, CancellationToken ct = default);

    Task<ChatAnswer> AskAsync(
        string question, List<RetrievedSegment> context, List<ChatTurn>? history = null,
        CancellationToken ct = default);

    Task<string> AskGeneralAsync(
        string question, List<ChatTurn>? history = null, CancellationToken ct = default);
}
