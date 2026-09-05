using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;
using UglyToad.PdfPig;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IAuditService _audit;
    private readonly IDocumentIndexingService _indexing;
    private readonly IDocumentCompetenceService _competences;
    private readonly ILogger<DocumentController> _logger;

    private static readonly string[] AllowedExtensions = { ".pdf" };
    private const long MaxFileSize = 20 * 1024 * 1024;

    public DocumentController(ApplicationDbContext db, IWebHostEnvironment env, IAuditService audit,
        IDocumentIndexingService indexing, IDocumentCompetenceService competences,
        ILogger<DocumentController> logger)
    {
        _db = db; _env = env; _audit = audit; _indexing = indexing;
        _competences = competences; _logger = logger;
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin() => User.IsInRole("ADMINISTRATEUR");
    private string BaseUrl() => $"{Request.Scheme}://{Request.Host}";

    private static DocumentDto ToDto(Document d, string baseUrl) => new(
        d.Id, d.Titre, d.TypeDocument,
        d.CategorieId, d.Categorie?.Nom,
        d.UploadedBy, d.Uploader?.Nom ?? "",
        d.Resume, d.Langue, d.NombrePages, d.TailleFichier,
        d.StatutTraitement.ToString(), d.DateAjout,
        baseUrl + d.CheminFichier
    );

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<DocumentDto>>> GetAll(
        [FromQuery] int? categorieId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Documents.AsNoTracking()
            .Include(d => d.Categorie).Include(d => d.Uploader).AsQueryable();

        if (!IsAdmin()) query = query.Where(d => d.UploadedBy == CurrentUserId());
        if (categorieId.HasValue) query = query.Where(d => d.CategorieId == categorieId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(d => d.Titre.Contains(search));

        var total = await query.CountAsync();
        var baseUrl = BaseUrl();
        var docs = await query
            .OrderByDescending(d => d.DateAjout)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResult<DocumentDto>(docs.Select(d => ToDto(d, baseUrl)), total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentDto>> GetOne(int id)
    {
        var doc = await _db.Documents.AsNoTracking()
            .Include(d => d.Categorie).Include(d => d.Uploader)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc is null) return NotFound(new { message = "Document not found." });
        if (!IsAdmin() && doc.UploadedBy != CurrentUserId()) return Forbid();
        return Ok(ToDto(doc, BaseUrl()));
    }

    public class UploadRequest
    {
        public string Titre { get; set; } = "";
        public IFormFile File { get; set; } = null!;
        public int? CategorieId { get; set; }
        public string? TypeDocument { get; set; }
        public string? Langue { get; set; }
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] UploadRequest req)
    {
        var file = req.File;
        var titre = req.Titre;
        var categorieId = req.CategorieId;
        var typeDocument = req.TypeDocument;
        var langue = req.Langue;

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file received." });
        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "File too large (20 MB maximum)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Only PDF files are accepted." });

        if (!await FileValidationHelper.IsPdfAsync(file))
            return BadRequest(new { message = "File content is not a valid PDF." });

        if (string.IsNullOrWhiteSpace(titre))
            return BadRequest(new { message = "Title is required." });

        // Compute SHA-256 hash before touching disk, so a duplicate never gets written or stored twice
        string hashDocument;
        using (var uploadStream = file.OpenReadStream())
        {
            var hashBytes = await SHA256.HashDataAsync(uploadStream);
            hashDocument = Convert.ToHexString(hashBytes).ToLower();
        }

        var existing = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.HashDocument == hashDocument);
        if (existing is not null)
            return Conflict(new { message = $"Ce document existe déjà (\"{existing.Titre}\").", documentId = existing.Id });

        var folder = Path.Combine(_env.WebRootPath, "uploads", "documents");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream);

        int nombrePages = 0;
        try
        {
            using var pdfStream = System.IO.File.OpenRead(fullPath);
            using var pdf = PdfDocument.Open(pdfStream);
            nombrePages = pdf.NumberOfPages;
        }
        catch { /* non-critical — proceed even if page count fails */ }

        var userId = CurrentUserId();
        var doc = new Document
        {
            Titre = titre, CheminFichier = $"/uploads/documents/{fileName}",
            TypeDocument = typeDocument, CategorieId = categorieId,
            Langue = langue, TailleFichier = file.Length,
            NombrePages = nombrePages > 0 ? nombrePages : null,
            HashDocument = hashDocument,
            UploadedBy = userId, StatutTraitement = StatutTraitement.EN_ATTENTE,
            DateAjout = DateTime.UtcNow,
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        await _db.Entry(doc).Reference(d => d.Categorie).LoadAsync();
        await _db.Entry(doc).Reference(d => d.Uploader).LoadAsync();

        await _audit.LogAsync(userId, "UPLOAD_DOCUMENT", "document", doc.Id);

        try
        {
            await _indexing.ChunkDocumentAsync(doc.Id);
        }
        catch (Exception ex)
        {
            // Upload already succeeded — indexing failure only affects chatbot availability
            // for this document (reflected in StatutTraitement), so it must not fail the request.
            _logger.LogError(ex, "Failed to index document {DocumentId} after upload.", doc.Id);
        }

        return StatusCode(StatusCodes.Status201Created, ToDto(doc, BaseUrl()));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateDocumentRequest request)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound(new { message = "Document not found." });

        if (!IsAdmin() && doc.UploadedBy != CurrentUserId()) return Forbid();

        doc.Titre = request.Titre;
        doc.CategorieId = request.CategorieId;
        doc.TypeDocument = request.TypeDocument;
        doc.Langue = request.Langue;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(CurrentUserId(), "UPDATE_DOCUMENT", "document", id);
        return Ok(new { message = "Document updated successfully." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound(new { message = "Document not found." });

        if (!IsAdmin() && doc.UploadedBy != CurrentUserId()) return Forbid();

        var fullPath = Path.Combine(_env.WebRootPath,
            doc.CheminFichier.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

        var userId = CurrentUserId();
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(userId, "DELETE_DOCUMENT", "document", id);
        return Ok(new { message = "Document deleted successfully." });
    }

    // Manual re-index trigger — indexing now also runs automatically right after upload,
    // this lets an admin retry a document stuck in ERREUR or reprocess after a pipeline change.
    [HttpPost("{id:int}/chunk")]
    public async Task<IActionResult> Chunk(int id)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound(new { message = "Document not found." });
        if (!IsAdmin() && doc.UploadedBy != CurrentUserId()) return Forbid();

        var chunkCount = await _indexing.ChunkDocumentAsync(id);
        return Ok(new { message = $"{chunkCount} chunk(s) created.", chunkCount });
    }

    // Standalone entry point for the document card's competences popup — same extraction logic
    // the chatbot uses conversationally, callable directly without going through the chat.
    [HttpGet("{id:int}/competences")]
    public async Task<IActionResult> GetCompetences(int id)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return NotFound(new { message = "Document not found." });
        if (!IsAdmin() && doc.UploadedBy != CurrentUserId()) return Forbid();

        if (doc.StatutTraitement != StatutTraitement.DISPONIBLE)
            return Ok(new { competences = Array.Empty<object>(), ready = false });

        var segments = await _db.DocumentSegments.AsNoTracking()
            .Where(s => s.DocumentId == id)
            .OrderBy(s => s.Ordre)
            .ToListAsync();

        if (segments.Count == 0)
            return Ok(new { competences = Array.Empty<object>(), ready = false });

        var competences = await _competences.ExtractCompetencesAsync(doc, segments);

        return Ok(new
        {
            competences = competences.Select(c => new { c.Libelle, c.Domaine }),
            ready = true
        });
    }
}
