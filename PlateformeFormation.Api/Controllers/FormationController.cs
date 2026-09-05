using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;
using static PlateformeFormation.Api.Helpers.FormationContentParser;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/formations")]
[Authorize]
public class FormationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRetrievalService _retrieval;
    private readonly IFormationGenerationService _generation;
    private readonly IFormationExportService _export;
    private readonly IFormationPptxExportService _pptxExport;
    private readonly IFormationQualityService _quality;
    private readonly IFormationTraceabilityService _traceability;
    private readonly IFormationCorrectionService _correction;
    private readonly IFormationModuleRegenerationService _moduleRegeneration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FormationController> _logger;

    public FormationController(
        ApplicationDbContext db, IAuditService audit, IRetrievalService retrieval,
        IFormationGenerationService generation, IFormationExportService export, IFormationPptxExportService pptxExport,
        IFormationQualityService quality, IFormationTraceabilityService traceability,
        IFormationCorrectionService correction, IFormationModuleRegenerationService moduleRegeneration,
        IWebHostEnvironment env, ILogger<FormationController> logger)
    {
        _db = db; _audit = audit; _retrieval = retrieval; _generation = generation;
        _export = export; _pptxExport = pptxExport; _quality = quality; _traceability = traceability; _correction = correction;
        _moduleRegeneration = moduleRegeneration; _env = env; _logger = logger;
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => User.IsInRole("ADMINISTRATEUR");

    private FormationDto ToDto(Formation f)
    {
        var report = _quality.Evaluate(f);
        var modules = ParseCards<ModuleCard>(f.Modules, () => new ModuleCard(0, f.Modules, null, null, null, null, null, null, null, null, null, null, null));
        var moduleBonus = ParseObject<ObjectifsData>(f.Objectifs)?.ModuleBonus;
        return new(
            f.Id, f.Titre, f.Objectifs, f.DureeEstimee, f.Modules, f.Activites, f.MethodesEvaluation,
            f.Statut.ToString(), f.CreePar, f.Createur?.Nom ?? "", f.DateCreation,
            f.FormationDocuments
                .OrderByDescending(fd => fd.ScorePertinence)
                .Select(fd => new FormationDocumentDto(fd.DocumentId, fd.Document?.Titre ?? "", fd.ScorePertinence))
                .ToList(),
            report.Score, report.Niveau, FormationPlanner.ComputeJours(modules, moduleBonus)
        );
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<FormationDto>>> GetAll(
        [FromQuery] string? statut, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Formations.AsNoTracking()
            .Include(f => f.Createur)
            .Include(f => f.FormationDocuments).ThenInclude(fd => fd.Document)
            .AsQueryable();

        if (!IsAdmin()) query = query.Where(f => f.CreePar == CurrentUserId());
        if (!string.IsNullOrWhiteSpace(statut) && Enum.TryParse<StatutFormation>(statut, true, out var statutFilter))
            query = query.Where(f => f.Statut == statutFilter);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.DateCreation)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResult<FormationDto>(items.Select(ToDto), total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FormationDto>> GetOne(int id)
    {
        var f = await _db.Formations.AsNoTracking()
            .Include(x => x.Createur)
            .Include(x => x.FormationDocuments).ThenInclude(fd => fd.Document)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();
        return Ok(ToDto(f));
    }

    [HttpGet("{id:int}/qualite")]
    public async Task<ActionResult<FormationQualityReport>> GetQualite(int id)
    {
        var f = await _db.Formations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();
        return Ok(_quality.Evaluate(f));
    }

    // Semantic-search-driven document selection — reuses the same embeddings the chatbot uses,
    // so the generated plan is grounded in documents actually relevant to the stated objective.
    private async Task<List<(Document Doc, float Score)>> SelectCandidateDocumentsAsync(
        string objectif, List<int>? explicitIds, CancellationToken ct)
    {
        var accessibleQuery = _db.Documents.Where(d => d.StatutTraitement == StatutTraitement.DISPONIBLE);
        if (!IsAdmin()) accessibleQuery = accessibleQuery.Where(d => d.UploadedBy == CurrentUserId());

        if (explicitIds is { Count: > 0 })
        {
            var docs = await accessibleQuery.Where(d => explicitIds.Contains(d.Id)).ToListAsync(ct);
            var result = new List<(Document, float)>();
            foreach (var doc in docs)
            {
                var matches = await _retrieval.SearchAsync(objectif, topK: 1, documentId: doc.Id, minScore: 0f, ct: ct);
                result.Add((doc, matches.Count > 0 ? matches[0].Score : 0.5f));
            }
            return result;
        }

        var accessibleIds = (await accessibleQuery.Select(d => d.Id).ToListAsync(ct)).ToHashSet();
        if (accessibleIds.Count == 0) return [];

        var broad = await _retrieval.SearchAsync(objectif, topK: 40, ct: ct);
        var grouped = broad
            .Where(r => accessibleIds.Contains(r.DocumentId))
            .GroupBy(r => r.DocumentId)
            .Select(g => new { DocumentId = g.Key, Score = g.Max(x => x.Score) })
            .OrderByDescending(x => x.Score)
            .ToList();

        if (grouped.Count == 0) return [];

        // Cosine scores in this embedding space are compressed (a genuinely relevant chunk often
        // scores only ~0.35-0.4), so an absolute floor barely filters anything and effectively
        // selects nearly every document regardless of the objective. Keep only documents whose
        // relevance is close to the best match found for THIS objective, not just above a low
        // fixed number — this is what actually differentiates "relevant" from "everything".
        var topScore = grouped[0].Score;
        var topDocIds = grouped
            .Where(x => x.Score >= topScore - 0.08f)
            .Take(5)
            .ToList();

        var chosenDocs = await _db.Documents
            .Where(d => topDocIds.Select(t => t.DocumentId).Contains(d.Id))
            .ToListAsync(ct);

        return chosenDocs
            .Select(d => (d, topDocIds.First(t => t.DocumentId == d.Id).Score))
            .ToList();
    }

    // Always uses the full segment text, never the cached chatbot Resume — the Resume is a short
    // 3-6 point summary meant for a quick chat answer, and using it here starves the formation
    // generator of the specific facts/details it needs to avoid generic output.
    private async Task<string> GetDocumentContentAsync(Document doc, CancellationToken ct)
    {
        var segments = await _db.DocumentSegments.AsNoTracking()
            .Where(s => s.DocumentId == doc.Id)
            .OrderBy(s => s.Ordre)
            .ToListAsync(ct);

        return string.Join("\n\n", segments.Select(s => s.ContenuTexte));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<FormationDto>> Generate(GenerateFormationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Objectif))
            return BadRequest(new { message = "L'objectif est requis." });

        if (req.DocumentIds is { Count: > 0 })
        {
            var accessibleQuery = _db.Documents.Where(d => d.StatutTraitement == StatutTraitement.DISPONIBLE);
            if (!IsAdmin()) accessibleQuery = accessibleQuery.Where(d => d.UploadedBy == CurrentUserId());
            var foundCount = await accessibleQuery.CountAsync(d => req.DocumentIds.Contains(d.Id), ct);
            if (foundCount != req.DocumentIds.Count)
                return BadRequest(new { message = "Un ou plusieurs documents sont introuvables ou non indexés." });
        }

        var candidates = await SelectCandidateDocumentsAsync(req.Objectif, req.DocumentIds, ct);
        if (candidates.Count == 0)
            return BadRequest(new
            {
                message = "Aucun document pertinent trouvé pour cet objectif. Importez des documents ou reformulez l'objectif."
            });

        var sources = new List<FormationSourceDocument>();
        foreach (var (doc, _) in candidates)
            sources.Add(new FormationSourceDocument(doc.Id, doc.Titre, await GetDocumentContentAsync(doc, ct)));

        FormationDraft draft;
        try
        {
            draft = await _generation.GenerateAsync(req.Objectif, sources, ct: ct);
        }
        catch (FormationValidationFailedException ex)
        {
            // Explicit, detailed failure — never fall through to a silently-saved BROUILLON with
            // known errors. Logged server-side (the "log détaillé" the spec asks for) since the
            // detail also goes back to the caller here, unlike the generic 500 handler which would
            // swallow it.
            _logger.LogWarning(ex, "Génération de formation invalide après plusieurs tentatives (objectif: {Objectif})", req.Objectif);
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Groq"))
        {
            // A provider-side failure (rate limit, outage, auth) is a different problem than invalid
            // content — surfacing it distinctly avoids the generic 500 handler's "unexpected error"
            // message, which is indistinguishable from an actual bug to the person using the form.
            _logger.LogError(ex, "Échec de l'appel au service de génération (objectif: {Objectif})", req.Objectif);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Le service de génération est temporairement indisponible (quota ou limite de requêtes atteinte). Réessayez dans quelques minutes." });
        }

        // Best-effort — a traceability failure must not lose an otherwise successful generation.
        // The service itself already tolerates per-module retrieval failures; this outer guard
        // covers anything unexpected (e.g. a malformed modules JSON) so Generate never regresses.
        string modulesWithSources;
        try
        {
            modulesWithSources = await _traceability.AttachSourcesAsync(
                draft.Modules, candidates.Select(c => c.Doc.Id).ToList(), ct);
        }
        catch
        {
            modulesWithSources = draft.Modules;
        }

        var formation = new Formation
        {
            Titre = draft.Titre,
            Objectifs = draft.Objectifs,
            DureeEstimee = draft.DureeEstimeeHeures,
            Modules = modulesWithSources,
            Activites = draft.Activites,
            MethodesEvaluation = draft.MethodesEvaluation,
            Statut = StatutFormation.BROUILLON,
            CreePar = CurrentUserId(),
            DateCreation = DateTime.UtcNow,
        };

        foreach (var (doc, score) in candidates)
        {
            formation.FormationDocuments.Add(new FormationDocument
            {
                DocumentId = doc.Id,
                ScorePertinence = (decimal)Math.Clamp(score, 0f, 1f)
            });
        }

        _db.Formations.Add(formation);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(CurrentUserId(), "GENERATE_FORMATION", "formation", formation.Id);

        await _db.Entry(formation).Reference(f => f.Createur).LoadAsync(ct);
        await _db.Entry(formation).Collection(f => f.FormationDocuments).Query().Include(fd => fd.Document).LoadAsync(ct);

        return Ok(ToDto(formation));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FormationDto>> Update(int id, UpdateFormationRequest req)
    {
        var f = await _db.Formations
            .Include(x => x.Createur)
            .Include(x => x.FormationDocuments).ThenInclude(fd => fd.Document)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        f.Titre = req.Titre;
        f.Objectifs = req.Objectifs;
        f.DureeEstimee = req.DureeEstimee;
        f.Modules = req.Modules;
        f.Activites = req.Activites;
        f.MethodesEvaluation = req.MethodesEvaluation;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUserId(), "UPDATE_FORMATION", "formation", id);
        return Ok(ToDto(f));
    }

    [HttpPut("{id:int}/statut")]
    public async Task<ActionResult<FormationDto>> UpdateStatut(int id, UpdateFormationStatutRequest req)
    {
        if (!Enum.TryParse<StatutFormation>(req.Statut, true, out var statut))
            return BadRequest(new { message = "Statut invalide." });

        var f = await _db.Formations
            .Include(x => x.Createur)
            .Include(x => x.FormationDocuments).ThenInclude(fd => fd.Document)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        f.Statut = statut;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUserId(), "UPDATE_FORMATION_STATUT", "formation", id);
        return Ok(ToDto(f));
    }

    // Catch-up pass for formations generated before traceability existed (or whose first attempt
    // failed) — otherwise the feature would only ever be visible on brand new generations.
    [HttpPost("{id:int}/traces")]
    public async Task<ActionResult<FormationDto>> AttachTraces(int id, CancellationToken ct)
    {
        var f = await _db.Formations
            .Include(x => x.Createur)
            .Include(x => x.FormationDocuments).ThenInclude(fd => fd.Document)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        var documentIds = f.FormationDocuments.Select(fd => fd.DocumentId).ToList();
        f.Modules = await _traceability.AttachSourcesAsync(f.Modules ?? "[]", documentIds, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(CurrentUserId(), "ATTACH_FORMATION_TRACES", "formation", id);
        return Ok(ToDto(f));
    }

    // Preview only — nothing is saved here. Computes what a correction would look like (deterministic
    // arithmetic always, a targeted Groq rewrite only if content-judgment problems remain — see
    // FormationCorrectionService) and returns both versions with their scores so the user can compare
    // before deciding. No source documents needed (the correction only rewrites existing content), so
    // this is a fast, DB-light call — applying it reuses the normal PUT /formations/{id} update flow.
    [HttpPost("{id:int}/corriger")]
    public async Task<ActionResult<FormationCorrectionPreviewDto>> PreviewCorrection(int id, CancellationToken ct)
    {
        var f = await _db.Formations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        var avantReport = _quality.Evaluate(f);
        var draft = await _correction.CorrectAsync(f, ct);

        var apres = new Formation
        {
            Objectifs = draft.Objectifs, DureeEstimee = draft.DureeEstimeeHeures, Modules = draft.Modules,
            Activites = draft.Activites, MethodesEvaluation = draft.MethodesEvaluation,
        };
        var apresReport = _quality.Evaluate(apres);

        var avantModules = ParseCards<ModuleCard>(f.Modules, () => new ModuleCard(0, f.Modules, null, null, null, null, null, null, null, null, null, null, null));
        var apresModules = ParseCards<ModuleCard>(draft.Modules, () => new ModuleCard(0, draft.Modules, null, null, null, null, null, null, null, null, null, null, null));
        var avantBonus = ParseObject<ObjectifsData>(f.Objectifs)?.ModuleBonus;
        var apresBonus = ParseObject<ObjectifsData>(draft.Objectifs)?.ModuleBonus;

        return Ok(new FormationCorrectionPreviewDto(
            new FormationContentDto(f.Objectifs, f.DureeEstimee, f.Modules, f.Activites, f.MethodesEvaluation,
                avantReport.Score, avantReport.Niveau, FormationPlanner.ComputeJours(avantModules, avantBonus)),
            new FormationContentDto(draft.Objectifs, draft.DureeEstimeeHeures, draft.Modules, draft.Activites, draft.MethodesEvaluation,
                apresReport.Score, apresReport.Niveau, FormationPlanner.ComputeJours(apresModules, apresBonus))
        ));
    }

    // Preview only, same contract as PreviewCorrection above — nothing is persisted here. The caller
    // applies an accepted regeneration by merging it into the modules array and going through the
    // normal PUT /formations/{id} update flow.
    [HttpPost("{id:int}/modules/{numero:int}/regenerer")]
    public async Task<ActionResult<RegenerateModuleResultDto>> RegenerateModule(int id, int numero, CancellationToken ct)
    {
        var f = await _db.Formations
            .Include(x => x.Createur)
            .Include(x => x.FormationDocuments).ThenInclude(fd => fd.Document)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        var modules = ParseCards<ModuleCard>(f.Modules, () => new ModuleCard(0, f.Modules, null, null, null, null, null, null, null, null, null, null, null));
        var avant = modules.FirstOrDefault(m => m.Numero == numero);
        if (avant is null) return NotFound(new { message = $"Module {numero} introuvable." });

        var sources = new List<FormationSourceDocument>();
        foreach (var fd in f.FormationDocuments)
        {
            if (fd.Document is null) continue;
            sources.Add(new FormationSourceDocument(fd.Document.Id, fd.Document.Titre, await GetDocumentContentAsync(fd.Document, ct)));
        }

        ModuleCard apres;
        try
        {
            apres = await _moduleRegeneration.RegenerateAsync(f, numero, sources, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Module {numero} introuvable." });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Groq"))
        {
            _logger.LogError(ex, "Échec de la régénération du module {Numero} (formation {Id})", numero, id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Le service de génération est temporairement indisponible (quota ou limite de requêtes atteinte). Réessayez dans quelques minutes." });
        }

        // Best-effort — GetModulesJson (via MapModule) never emits "sources", so without this the
        // regenerated module would lose its traceability chip. A failure here must not lose an
        // otherwise successful regeneration.
        try
        {
            var documentIds = f.FormationDocuments.Select(fd => fd.DocumentId).ToList();
            var withSourceJson = await _traceability.AttachSourcesAsync(
                JsonSerializer.Serialize(new[] { apres }, JsonWriteOpts), documentIds, ct);
            apres = ParseCards<ModuleCard>(withSourceJson, () => apres).FirstOrDefault() ?? apres;
        }
        catch
        {
            // keep apres as-is
        }

        await _audit.LogAsync(CurrentUserId(), "REGENERATE_FORMATION_MODULE", "formation", id);
        return Ok(new RegenerateModuleResultDto(avant, apres));
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id)
    {
        var f = await _db.Formations
            .Include(x => x.Createur)
            .Include(x => x.FormationDocuments).ThenInclude(fd => fd.Document)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        var pdfBytes = _export.GeneratePdf(f);

        var folder = Path.Combine(_env.WebRootPath, "uploads", "exports");
        Directory.CreateDirectory(folder);
        var fileName = $"formation-{id}-{Guid.NewGuid():N}.pdf";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, fileName), pdfBytes);

        _db.ExportsHistorique.Add(new ExportHistorique
        {
            FormationId = id,
            UtilisateurId = CurrentUserId(),
            Format = FormatExport.PDF,
            CheminFichier = $"/uploads/exports/{fileName}",
            DateExport = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUserId(), "EXPORT_FORMATION", "formation", id);

        var safeTitle = string.Join("-", f.Titre.Split(Path.GetInvalidFileNameChars())).Trim('-');
        return File(pdfBytes, "application/pdf", $"{(string.IsNullOrWhiteSpace(safeTitle) ? "formation" : safeTitle)}.pdf");
    }

    // Curated presentation deck (title, agenda, planning, one slide per module, évaluation,
    // ressources/sources) — a deliberately shorter, presentable sibling to the PDF's full 13-section
    // document. See FormationPptxExportService for what's included and what's PDF-only.
    [HttpGet("{id:int}/export/pptx")]
    public async Task<IActionResult> ExportPptx(int id)
    {
        var f = await _db.Formations
            .Include(x => x.Createur)
            .Include(x => x.FormationDocuments).ThenInclude(fd => fd.Document)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        var pptxBytes = _pptxExport.GeneratePptx(f);

        var folder = Path.Combine(_env.WebRootPath, "uploads", "exports");
        Directory.CreateDirectory(folder);
        var fileName = $"formation-{id}-{Guid.NewGuid():N}.pptx";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, fileName), pptxBytes);

        _db.ExportsHistorique.Add(new ExportHistorique
        {
            FormationId = id,
            UtilisateurId = CurrentUserId(),
            Format = FormatExport.PPTX,
            CheminFichier = $"/uploads/exports/{fileName}",
            DateExport = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUserId(), "EXPORT_FORMATION_PPTX", "formation", id);

        var safeTitle = string.Join("-", f.Titre.Split(Path.GetInvalidFileNameChars())).Trim('-');
        return File(pptxBytes, "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            $"{(string.IsNullOrWhiteSpace(safeTitle) ? "formation" : safeTitle)}.pptx");
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var f = await _db.Formations.FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return NotFound(new { message = "Formation not found." });
        if (!IsAdmin() && f.CreePar != CurrentUserId()) return Forbid();

        _db.Formations.Remove(f);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(CurrentUserId(), "DELETE_FORMATION", "formation", id);
        return NoContent();
    }
}
