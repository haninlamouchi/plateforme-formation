namespace PlateformeFormation.Api.Models;

public class Message
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public Emetteur Emetteur { get; set; }
    public string Contenu { get; set; } = string.Empty;
    public string? SourcesDocuments { get; set; }
    public DateTime DateEnvoi { get; set; } = DateTime.UtcNow;
}