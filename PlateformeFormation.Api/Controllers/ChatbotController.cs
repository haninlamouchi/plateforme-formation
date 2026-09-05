using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/chatbot")]
[Authorize]
public class ChatbotController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IRetrievalService _retrieval;
    private readonly IChatbotService _chatbot;
    private readonly IDocumentSummaryService _summary;
    private readonly IDocumentCompetenceService _competences;

    public ChatbotController(
        ApplicationDbContext db, IRetrievalService retrieval, IChatbotService chatbot,
        IDocumentSummaryService summary, IDocumentCompetenceService competences)
    {
        _db = db; _retrieval = retrieval; _chatbot = chatbot;
        _summary = summary; _competences = competences;
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => User.IsInRole("ADMINISTRATEUR");

    public class HistoryTurnRequest
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class AskRequest
    {
        public string Question { get; set; } = "";
        public int? DocumentId { get; set; }
        public int? CategorieId { get; set; }
        public List<HistoryTurnRequest>? History { get; set; }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask(AskRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest(new { message = "Question is required." });

        if (req.DocumentId.HasValue)
        {
            var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == req.DocumentId);
            if (doc is null) return NotFound(new { message = "Document not found." });
            if (!IsAdmin() && doc.UploadedBy != CurrentUserId()) return Forbid();
        }

        var history = req.History?
            .Where(h => h.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(h.Content))
            .Select(h => new ChatTurn(h.Role, h.Content))
            .ToList();

        var intent = ChatIntentDetector.Detect(req.Question);

        if (intent != ChatIntent.Question)
            return await HandleDocumentLevelIntent(intent, req, history);

        // Complex questions can cover several concepts. Generate up to three standalone search
        // angles, then fuse their evidence. This also resolves the subject of follow-up questions.
        // Ownership restriction passed into SearchAsync itself (applied before ranking/Take), not
        // filtered from the results afterward — the latter could silently return fewer than the
        // intended candidate count to a non-admin even when they own plenty of relevant documents.
        List<int>? allowedDocumentIds = null;
        if (!IsAdmin())
        {
            allowedDocumentIds = await _db.Documents.AsNoTracking()
                .Where(d => d.UploadedBy == CurrentUserId())
                .Select(d => d.Id)
                .ToListAsync();
        }

        var retrievalQueries = await _chatbot.BuildRetrievalQueriesAsync(req.Question, history, HttpContext.RequestAborted);
        var bestBySegment = new Dictionary<int, RetrievedSegment>();
        foreach (var query in retrievalQueries)
        {
            var matches = await _retrieval.SearchAsync(
                query, topK: 5, req.DocumentId, req.CategorieId, minScore: 0.30f,
                allowedDocumentIds: allowedDocumentIds, ct: HttpContext.RequestAborted);

            foreach (var match in matches)
            {
                if (!bestBySegment.TryGetValue(match.SegmentId, out var previous) || match.Score > previous.Score)
                    bestBySegment[match.SegmentId] = match;
            }
        }

        var results = bestBySegment.Values
            .OrderByDescending(r => r.Score)
            .Take(10)
            .ToList();

        // This embedding model's cosine scores are compressed (a genuinely relevant chunk often only
        // reaches ~0.35-0.4 — see SelectCandidateDocumentsAsync's comment on the same characteristic),
        // so the 0.30 floor above barely filters anything: a short, content-free question like "1+1"
        // can still "match" some segment on noise alone. For a question that doesn't even look
        // document-specific, only trust that match if it clears a much higher bar than a real document
        // question would need — otherwise treat it exactly like finding nothing at all.
        const float HighConfidenceScore = 0.55f;
        var bestScore = results.Count > 0 ? results[0].Score : 0f;
        if (results.Count == 0 || (!LooksDocumentSpecific(req.Question) && bestScore < HighConfidenceScore))
        {
            // Never replace an explicit request about an imported resource with an unsupported
            // general answer. General mode is reserved for genuinely independent questions.
            if (LooksDocumentSpecific(req.Question))
            {
                const string noMatchAnswer = "Je n'ai trouvé aucun passage pertinent dans les documents indexés pour répondre à cette question.";
                await PersistTurnAsync(req.Question, noMatchAnswer, "documents", [], HttpContext.RequestAborted);
                return Ok(new
                {
                    answer = noMatchAnswer,
                    mode = "documents",
                    sources = Array.Empty<object>()
                });
            }

            var generalAnswer = await _chatbot.AskGeneralAsync(
                req.Question, history, HttpContext.RequestAborted);

            await PersistTurnAsync(req.Question, generalAnswer, "general", [], HttpContext.RequestAborted);
            return Ok(new
            {
                answer = generalAnswer,
                mode = "general",
                sources = Array.Empty<object>()
            });
        }

        var answer = await _chatbot.AskAsync(req.Question, results, history, HttpContext.RequestAborted);
        var sourceTitles = answer.Sources.Select(s => s.DocumentTitre).Distinct().ToList();
        await PersistTurnAsync(req.Question, answer.Answer, "documents", sourceTitles, HttpContext.RequestAborted);

        return Ok(new
        {
            answer = answer.Answer,
            mode = "documents",
            sources = answer.Sources.Select(s => new
            {
                s.DocumentId,
                s.DocumentTitre,
                s.Ordre,
                s.NumeroPage,
                s.Score,
                extrait = BuildExcerpt(s.ContenuTexte)
            })
        });
    }

    // Analytics-only side effect (usage dashboard: questions/mode over time) — writes into the
    // Conversation/Message schema that already existed for exactly this purpose but was never wired
    // up. A persistence failure must never break the chatbot response itself, same tolerance already
    // used elsewhere in this controller and across the formation services (traceability attach, etc.).
    private async Task PersistTurnAsync(string question, string answer, string mode, List<string> sources, CancellationToken ct)
    {
        try
        {
            var userId = CurrentUserId();
            var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.UtilisateurId == userId, ct);
            if (conversation is null)
            {
                conversation = new Conversation { UtilisateurId = userId };
                _db.Conversations.Add(conversation);
            }

            _db.Messages.Add(new Message { Conversation = conversation, Emetteur = Emetteur.UTILISATEUR, Contenu = question });
            _db.Messages.Add(new Message
            {
                Conversation = conversation,
                Emetteur = Emetteur.ASSISTANT,
                Contenu = answer,
                SourcesDocuments = JsonSerializer.Serialize(new { mode, sources }),
            });
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Best-effort — see comment above.
        }
    }

    // Résumé / compétences need the whole document, not a top-K semantic match — resolve which
    // document the user means, then run the dedicated Groq-backed service over all its segments.
    private async Task<IActionResult> HandleDocumentLevelIntent(
        ChatIntent intent, AskRequest req, List<ChatTurn>? history)
    {
        var accessibleDocs = await _db.Documents
            .Where(d => IsAdmin() || d.UploadedBy == CurrentUserId())
            .ToListAsync();

        var target = ResolveDocument(req.Question, req.DocumentId, accessibleDocs, history);

        if (target is null)
        {
            var suggestion = accessibleDocs.Count is > 0 and <= 10
                ? " Vos documents disponibles : " + string.Join(", ", accessibleDocs.Select(d => d.Titre)) + "."
                : "";
            return Ok(new
            {
                answer = "Je peux faire ça, mais je n'ai pas identifié de quel document il s'agit. " +
                          "Pouvez-vous préciser son titre ?" + suggestion,
                sources = Array.Empty<object>()
            });
        }

        if (target.StatutTraitement != StatutTraitement.DISPONIBLE)
        {
            return Ok(new
            {
                answer = $"Le document \"{target.Titre}\" n'est pas encore indexé, réessayez dans quelques instants.",
                sources = Array.Empty<object>()
            });
        }

        var segments = await _db.DocumentSegments.AsNoTracking()
            .Where(s => s.DocumentId == target.Id)
            .OrderBy(s => s.Ordre)
            .ToListAsync();

        if (segments.Count == 0)
        {
            return Ok(new
            {
                answer = $"Le document \"{target.Titre}\" ne contient pas encore de contenu exploitable.",
                sources = Array.Empty<object>()
            });
        }

        var sources = new[] { new { DocumentId = target.Id, DocumentTitre = target.Titre, Ordre = 0, NumeroPage = (int?)null, Score = 1f, extrait = (string?)null } };

        if (intent == ChatIntent.Resume)
        {
            var summary = await _summary.GetOrGenerateSummaryAsync(target, segments);
            return Ok(new { answer = summary, sources });
        }

        var competences = await _competences.ExtractCompetencesAsync(target, segments);
        var answerText = competences.Count > 0
            ? $"Voici les compétences abordées dans le document \"{target.Titre}\"."
            : $"Je n'ai pas identifié de compétences claires dans le document \"{target.Titre}\".";

        return Ok(new
        {
            answer = answerText,
            sources,
            competences = competences.Select(c => new { c.Libelle, c.Domaine })
        });
    }

    private static Document? ResolveDocument(
        string question, int? explicitDocumentId, List<Document> candidates, List<ChatTurn>? history = null)
    {
        if (explicitDocumentId.HasValue)
            return candidates.FirstOrDefault(d => d.Id == explicitDocumentId.Value);

        if (candidates.Count == 1)
            return candidates[0];

        if (candidates.Count == 0)
            return null;

        // A request such as "resume-le" usually identifies its document in the immediately
        // preceding turn. Including a short history preserves that natural conversation flow.
        var conversationTail = string.Join(' ', history?.TakeLast(4).Select(h => h.Content) ?? []);
        var normQuestion = Normalize(question + " " + conversationTail);

        var exactMatch = candidates.FirstOrDefault(d =>
            normQuestion.Contains(Normalize(System.IO.Path.GetFileNameWithoutExtension(d.Titre))));
        if (exactMatch is not null)
            return exactMatch;

        Document? best = null;
        double bestScore = 0;
        foreach (var d in candidates)
        {
            var titleWords = Normalize(System.IO.Path.GetFileNameWithoutExtension(d.Titre))
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .ToList();
            if (titleWords.Count == 0) continue;

            var matched = titleWords.Count(w => normQuestion.Contains(w));
            var score = (double)matched / titleWords.Count;
            if (score > bestScore)
            {
                bestScore = score;
                best = d;
            }
        }

        return bestScore >= 0.5 ? best : null;
    }

    private static string Normalize(string s)
    {
        var decomposed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().ToLowerInvariant();
    }

    private static string BuildExcerpt(string text)
    {
        const int maxLength = 360;
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private static bool LooksDocumentSpecific(string question)
    {
        var q = Normalize(question);
        string[] markers =
        [
            "document", "pdf", "guide", "referentiel", "selon le", "selon ce", "dans le fichier",
            "dans ce fichier", "dans le support", "page ", "extrait", "source", "ce module", "ce texte"
        ];
        return markers.Any(q.Contains);
    }
}
