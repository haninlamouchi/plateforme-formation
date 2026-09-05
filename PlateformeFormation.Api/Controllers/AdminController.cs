using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;
using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/admin/utilisateurs")]
[Authorize(Roles = "ADMINISTRATEUR")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;
    private readonly IAuditService _audit;
    private readonly IFormationQualityService _quality;

    public AdminController(
        ApplicationDbContext db, IEmailService emailService, IConfiguration configuration,
        ILogger<AdminController> logger, IAuditService audit, IFormationQualityService quality)
    {
        _db = db; _emailService = emailService;
        _configuration = configuration; _logger = logger; _audit = audit; _quality = quality;
    }

    [HttpGet("/api/admin/stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats()
    {
        var activeUsers     = await _db.Utilisateurs.CountAsync(u => u.StatutCompte == StatutCompte.VALIDE && u.Actif && u.Role != RoleUtilisateur.ADMINISTRATEUR);
        var pendingAccounts = await _db.Utilisateurs.CountAsync(u => u.StatutCompte == StatutCompte.EN_ATTENTE);
        var totalDocuments  = await _db.Documents.CountAsync();
        var totalFormations = await _db.Formations.CountAsync();
        var totalCategories = await _db.Categories.CountAsync();
        var since           = DateTime.UtcNow.AddDays(-30);
        var recentUploads   = await _db.Documents.CountAsync(d => d.DateAjout >= since);

        return Ok(new AdminStatsDto(activeUsers, pendingAccounts, totalDocuments, totalFormations, totalCategories, recentUploads));
    }

    [HttpGet("/api/admin/charts")]
    public async Task<ActionResult<AdminChartsDto>> GetCharts()
    {
        // Docs per category (top 6)
        var docsByCategory = await (
            from d in _db.Documents
            where d.CategorieId != null
            join c in _db.Categories on d.CategorieId equals c.Id
            group d by c.Nom into g
            orderby g.Count() descending
            select new CategoryStatDto(g.Key, g.Count())
        ).Take(6).ToListAsync();

        // Active users by role (excluding admins)
        var usersByRole = await _db.Utilisateurs
            .Where(u => u.StatutCompte == StatutCompte.VALIDE && u.Actif && u.Role != RoleUtilisateur.ADMINISTRATEUR)
            .GroupBy(u => u.Role)
            .Select(g => new RoleStatDto(g.Key.ToString(), g.Count()))
            .ToListAsync();

        // Uploads per month — last 6 months (pulled to memory first to avoid EF date-part translation issues)
        var since = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var rawUploads = await _db.Documents
            .Where(d => d.DateAjout >= since)
            .Select(d => new { d.DateAjout.Year, d.DateAjout.Month })
            .ToListAsync();

        var uploadsByMonth = Enumerable.Range(0, 6)
            .Select(i => DateTime.UtcNow.AddMonths(i - 5))
            .Select(d => new MonthlyStatDto(
                d.ToString("MMM"),
                rawUploads.Count(u => u.Year == d.Year && u.Month == d.Month)
            ))
            .ToList();

        return Ok(new AdminChartsDto(docsByCategory, usersByRole, uploadsByMonth));
    }

    // JournalActivite is written to by every controller via IAuditService.LogAsync (upload, generation,
    // export, validation, etc.) but had no read endpoint before this — this is the first place it's
    // actually surfaced to an admin.
    [HttpGet("/api/admin/audit-log")]
    public async Task<ActionResult<PaginatedResult<AuditLogEntryDto>>> GetAuditLog(
        [FromQuery] string? action, [FromQuery] int? utilisateurId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.JournalActivites.AsNoTracking()
            .Include(j => j.Utilisateur)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(j => j.Action == action);
        if (utilisateurId.HasValue)
            query = query.Where(j => j.UtilisateurId == utilisateurId);

        var total = await query.CountAsync();
        var entries = await query
            .OrderByDescending(j => j.DateAction)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new AuditLogEntryDto(
                j.Id, j.Action, j.EntiteConcernee, j.EntiteId, j.DateAction,
                j.UtilisateurId, j.Utilisateur!.Nom, j.Utilisateur.Email))
            .ToListAsync();

        return Ok(new PaginatedResult<AuditLogEntryDto>(entries, total, page, pageSize));
    }

    [HttpGet("/api/admin/audit-log/actions")]
    public async Task<ActionResult<IEnumerable<string>>> GetAuditLogActions()
    {
        var actions = await _db.JournalActivites.AsNoTracking()
            .Select(j => j.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
        return Ok(actions);
    }

    // Extends /api/admin/stats + /api/admin/charts with usage/activity data those two don't cover:
    // formation-generation volume, export activity, chatbot usage, and quality trends.
    [HttpGet("/api/admin/analytics/summary")]
    public async Task<ActionResult<AdminAnalyticsSummaryDto>> GetAnalyticsSummary()
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var formationsThisMonth = await _db.JournalActivites
            .CountAsync(j => j.Action == "GENERATE_FORMATION" && j.DateAction >= startOfMonth);

        // Quality is computed on demand (FormationQualityService is pure C#, no LLM call) rather than
        // stored — same call FormationController.ToDto already makes per-formation.
        var formations = await _db.Formations.AsNoTracking().ToListAsync();
        var validationRate = formations.Count == 0 ? 0
            : 100.0 * formations.Count(f => f.Statut == StatutFormation.VALIDEE) / formations.Count;
        var avgQuality = formations.Count == 0 ? 0
            : formations.Average(f => _quality.Evaluate(f).Score);

        var chatbotQuestionsThisMonth = await _db.Messages
            .CountAsync(m => m.Emetteur == Emetteur.UTILISATEUR && m.DateEnvoi >= startOfMonth);

        // ExportHistorique, not the EXPORT_FORMATION*/EXPORT_FORMATION_PPTX audit actions — both record
        // the same event, and ExportHistorique is the more structured source (avoids double-counting).
        var exportsThisMonth = await _db.ExportsHistorique
            .Where(e => e.DateExport >= startOfMonth)
            .Select(e => e.Format)
            .ToListAsync();

        return Ok(new AdminAnalyticsSummaryDto(
            formationsThisMonth,
            Math.Round(validationRate, 1),
            Math.Round(avgQuality, 1),
            chatbotQuestionsThisMonth,
            exportsThisMonth.Count,
            exportsThisMonth.Count(f => f == FormatExport.PDF),
            exportsThisMonth.Count(f => f == FormatExport.PPTX)));
    }

    [HttpGet("/api/admin/analytics/charts")]
    public async Task<ActionResult<AdminAnalyticsChartsDto>> GetAnalyticsCharts()
    {
        var since = DateTime.UtcNow.Date.AddDays(-29); // 30-day window including today

        // Pulled to memory first, same as GetCharts.UploadsByMonth — Pomelo/MySQL date-part GroupBy
        // translation is unreliable here, already documented at that call site.
        var rawGenerations = await _db.JournalActivites
            .Where(j => j.Action == "GENERATE_FORMATION" && j.DateAction >= since)
            .Select(j => j.DateAction.Date)
            .ToListAsync();
        var rawExports = await _db.ExportsHistorique
            .Where(e => e.DateExport >= since)
            .Select(e => e.DateExport.Date)
            .ToListAsync();
        var rawQuestions = await _db.Messages
            .Where(m => m.Emetteur == Emetteur.UTILISATEUR && m.DateEnvoi >= since)
            .Select(m => m.DateEnvoi.Date)
            .ToListAsync();

        var days = Enumerable.Range(0, 30).Select(i => since.AddDays(i)).ToList();
        List<DailyStatDto> ToDaily(List<DateTime> raw) =>
            days.Select(d => new DailyStatDto(d.ToString("dd/MM"), raw.Count(x => x == d))).ToList();

        var timeline = new ActivityTimelineDto(ToDaily(rawGenerations), ToDaily(rawExports), ToDaily(rawQuestions));

        var topDocumentGroups = await _db.FormationDocuments
            .GroupBy(fd => fd.DocumentId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count(), AvgScore = g.Average(x => (double?)x.ScorePertinence) ?? 0 })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();
        var topDocumentIds = topDocumentGroups.Select(g => g.DocumentId).ToList();
        var documentTitles = await _db.Documents
            .Where(d => topDocumentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Titre);
        var topDocuments = topDocumentGroups
            .Select(g => new DocumentUsageStatDto(
                g.DocumentId, documentTitles.GetValueOrDefault(g.DocumentId, $"Document {g.DocumentId}"),
                g.Count, Math.Round(g.AvgScore, 2)))
            .ToList();

        // Mode was never a first-class column — it's parsed back out of the JSON PersistTurnAsync
        // (ChatbotController) writes into SourcesDocuments, so no schema change was needed for this.
        var assistantSourceBlobs = await _db.Messages
            .Where(m => m.Emetteur == Emetteur.ASSISTANT && m.SourcesDocuments != null)
            .Select(m => m.SourcesDocuments!)
            .ToListAsync();
        var chatbotModeSplit = assistantSourceBlobs
            .Select(json =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.TryGetProperty("mode", out var m) ? m.GetString() ?? "documents" : "documents";
                }
                catch (JsonException)
                {
                    return "documents";
                }
            })
            .GroupBy(mode => mode)
            .Select(g => new ChatbotModeStatDto(g.Key, g.Count()))
            .ToList();

        var draftFormations = await _db.Formations.AsNoTracking()
            .Where(f => f.Statut == StatutFormation.BROUILLON)
            .ToListAsync();
        var formationsNeedingAttention = draftFormations
            .Select(f => (Formation: f, Report: _quality.Evaluate(f)))
            .OrderBy(x => x.Report.Score)
            .Take(5)
            .Select(x => new FormationNeedingAttentionDto(x.Formation.Id, x.Formation.Titre, x.Report.Score, x.Report.Niveau))
            .ToList();

        return Ok(new AdminAnalyticsChartsDto(timeline, topDocuments, chatbotModeSplit, formationsNeedingAttention));
    }

    [HttpGet("en-attente")]
    public async Task<ActionResult<IEnumerable<PendingUserDto>>> GetPendingUsers()
    {
        var users = await _db.Utilisateurs
            .AsNoTracking()
            .Where(user => user.StatutCompte == StatutCompte.EN_ATTENTE)
            .OrderByDescending(user => user.DateCreation)
            .Select(user => new PendingUserDto(
                user.Id,
                user.Nom,
                user.Email,
                user.Role.ToString(),
                user.StatutCompte.ToString(),
                user.DateCreation,
                user.Discipline,
                user.Departement,
                user.Telephone
            ))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("{id:int}/valider")]
    public async Task<IActionResult> ValidateUser(int id)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound(new { message = "User not found." });

        var previousStatus = user.StatutCompte;
        user.StatutCompte = StatutCompte.VALIDE;
        user.Actif = true;
        user.DateValidation = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (previousStatus == StatutCompte.EN_ATTENTE)
        {
            try
            {
                var loginBaseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                await _emailService.SendAccountValidatedAsync(user, $"{loginBaseUrl}/login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send validation email to {Email}.", user.Email);
            }
        }

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(adminId, "ADMIN_VALIDATE_USER", "utilisateur", id);
        return Ok(new { message = "User validated successfully." });
    }

    [HttpPut("{id:int}/refuser")]
    public async Task<IActionResult> RejectUser(int id)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound(new { message = "User not found." });

        user.StatutCompte = StatutCompte.REFUSE;
        user.Actif = false;
        user.DateValidation = null;

        await _db.SaveChangesAsync();

        try
        {
            await _emailService.SendAccountRejectedAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send rejection email to {Email}.", user.Email);
        }

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(adminId, "ADMIN_REJECT_USER", "utilisateur", id);
        return Ok(new { message = "User rejected successfully." });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserListItemDto>>> GetAllUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? statut)
    {
        var query = _db.Utilisateurs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Nom.Contains(search) || u.Email.Contains(search));

        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<RoleUtilisateur>(role, out var roleEnum))
            query = query.Where(u => u.Role == roleEnum);

        if (!string.IsNullOrWhiteSpace(statut) && Enum.TryParse<StatutCompte>(statut, out var statutEnum))
            query = query.Where(u => u.StatutCompte == statutEnum);

        var users = await query
            .OrderByDescending(u => u.DateCreation)
            .Select(u => new UserListItemDto(
                u.Id, u.Nom, u.Email, u.Role.ToString(), u.StatutCompte.ToString(),
                u.Actif, u.DateCreation, u.DateValidation, u.DerniereConnexion,
                u.Discipline, u.Departement, u.Telephone, u.PhotoUrl
            ))
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserListItemDto>> GetUser(int id)
    {
        var user = await _db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "User not found." });

        return Ok(new UserListItemDto(
            user.Id, user.Nom, user.Email, user.Role.ToString(), user.StatutCompte.ToString(),
            user.Actif, user.DateCreation, user.DateValidation, user.DerniereConnexion,
            user.Discipline, user.Departement, user.Telephone, user.PhotoUrl
        ));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserRequest request)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "User not found." });

        if (!Enum.TryParse<RoleUtilisateur>(request.Role, out var newRole))
            return BadRequest(new { message = "Invalid role." });

        // Prevent removing or deactivating the last active administrator — covers both a role
        // demotion AND setting Actif=false on the same admin via this endpoint (the latter was an
        // uncaught gap: the guard used to only look at `newRole`, so UpdateUser could deactivate the
        // last admin as long as the role field stayed ADMINISTRATEUR).
        if (user.Role == RoleUtilisateur.ADMINISTRATEUR && (newRole != RoleUtilisateur.ADMINISTRATEUR || !request.Actif))
        {
            var otherAdminCount = await _db.Utilisateurs.CountAsync(u =>
                u.Role == RoleUtilisateur.ADMINISTRATEUR && u.Actif && u.Id != id);
            if (otherAdminCount == 0)
                return BadRequest(new { message = "Cannot demote or deactivate the last administrator." });
        }

        user.Nom = request.Nom;
        user.Role = newRole;
        user.Actif = request.Actif;
        await _db.SaveChangesAsync();

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(adminId, "ADMIN_UPDATE_USER", "utilisateur", id);
        return Ok(new { message = "User updated successfully." });
    }

    [HttpPut("{id:int}/desactiver")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "User not found." });

        // Same guard as UpdateUser — without it, this endpoint could lock the whole platform's admin
        // access by deactivating the last active administrator.
        if (user.Role == RoleUtilisateur.ADMINISTRATEUR)
        {
            var otherAdminCount = await _db.Utilisateurs.CountAsync(u =>
                u.Role == RoleUtilisateur.ADMINISTRATEUR && u.Actif && u.Id != id);
            if (otherAdminCount == 0)
                return BadRequest(new { message = "Cannot deactivate the last administrator." });
        }

        user.Actif = false;
        await _db.SaveChangesAsync();

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(adminId, "ADMIN_DEACTIVATE_USER", "utilisateur", id);
        return Ok(new { message = "User deactivated successfully." });
    }

    [HttpPut("{id:int}/reactiver")]
    public async Task<IActionResult> ReactivateUser(int id)
    {
        var user = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound(new { message = "User not found." });

        user.Actif = true;
        await _db.SaveChangesAsync();

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(adminId, "ADMIN_REACTIVATE_USER", "utilisateur", id);
        return Ok(new { message = "User reactivated successfully." });
    }
}