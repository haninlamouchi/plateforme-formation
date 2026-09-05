using System.Security.Claims;
using System.Text;
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
[Route("api/categories")]
[Authorize]
public class CategorieController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAiSuggestionService _ai;

    // Same limits DocumentController.Upload enforces — SuggestForDocument was missing them entirely,
    // letting any authenticated user post an arbitrarily large or non-PDF file straight into
    // PdfDocument.Open with no try/catch, which throws an unhandled exception on malformed content.
    private const long MaxFileSize = 20 * 1024 * 1024;

    public CategorieController(ApplicationDbContext db, IAuditService audit, IAiSuggestionService ai)
    {
        _db = db; _audit = audit; _ai = ai;
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("suggest")]
    [Authorize(Roles = "ADMINISTRATEUR")]
    public async Task<ActionResult> Suggest()
    {
        var existingNames = await _db.Categories.AsNoTracking().Select(c => c.Nom).ToListAsync();
        var suggestions = await _ai.SuggestCategoriesAsync(existingNames);
        return Ok(suggestions);
    }

    public class FileUploadRequest { public IFormFile File { get; set; } = null!; }

    [HttpPost("suggest-for-document")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> SuggestForDocument([FromForm] FileUploadRequest req)
    {
        var file = req.File;
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });
        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "File too large (20 MB maximum)." });
        if (Path.GetExtension(file.FileName).ToLowerInvariant() != ".pdf")
            return BadRequest(new { message = "Only PDF files are accepted." });
        if (!await FileValidationHelper.IsPdfAsync(file))
            return BadRequest(new { message = "File content is not a valid PDF." });

        string extractedText;
        try
        {
            extractedText = ExtractPdfText(file.OpenReadStream(), 8);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A file that passes the magic-byte check can still be a malformed/corrupt PDF that
            // PdfDocument.Open chokes on — surface it as a normal 400, not an unhandled 500.
            return BadRequest(new { message = "Could not read this PDF file." });
        }
        if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length < 30)
            return BadRequest(new { message = "Could not extract readable text from this PDF (may be scanned/image-based)." });

        var categories = await _db.Categories.AsNoTracking()
            .Select(c => new CategoryMatch(c.Id, c.Nom))
            .ToListAsync();

        var result = await _ai.SuggestCategoryForDocumentAsync(extractedText, categories);
        if (result is null)
            return Ok(new { categoryId = (int?)null, categoryNom = (string?)null, isNew = false });

        if (result.IsNew)
        {
            try
            {
                var newCat = new Categorie { Nom = result.CategoryNom, Description = null };
                _db.Categories.Add(newCat);
                await _db.SaveChangesAsync();
                await _audit.LogAsync(CurrentUserId(), "AI_CREATE_CATEGORY", "categorie", newCat.Id);
                return Ok(new { categoryId = newCat.Id, categoryNom = newCat.Nom, isNew = true });
            }
            catch (DbUpdateException)
            {
                // Concurrent request already created a category with this name — load it
                var existing = await _db.Categories.FirstOrDefaultAsync(c => c.Nom == result.CategoryNom);
                if (existing is not null)
                    return Ok(new { categoryId = existing.Id, categoryNom = existing.Nom, isNew = false });
                return StatusCode(500, new { message = "Failed to create category." });
            }
        }

        return Ok(new { categoryId = result.CategoryId, categoryNom = result.CategoryNom, isNew = false });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategorieDto>>> GetAll()
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Nom)
            .Select(c => new CategorieDto(c.Id, c.Nom, c.Description, c.Documents.Count))
            .ToListAsync();
        return Ok(categories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategorieDto>> GetOne(int id)
    {
        var c = await _db.Categories.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CategorieDto(x.Id, x.Nom, x.Description, x.Documents.Count))
            .FirstOrDefaultAsync();

        if (c is null) return NotFound(new { message = "Category not found." });
        return Ok(c);
    }

    [HttpPost]
    [Authorize(Roles = "ADMINISTRATEUR")]
    public async Task<ActionResult<CategorieDto>> Create(CreateCategorieRequest request)
    {
        if (await _db.Categories.AnyAsync(c => c.Nom == request.Nom))
            return BadRequest(new { message = "A category with this name already exists." });

        var cat = new Categorie { Nom = request.Nom, Description = request.Description };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(CurrentUserId(), "CREATE_CATEGORY", "categorie", cat.Id);
        return CreatedAtAction(nameof(GetOne), new { id = cat.Id },
            new CategorieDto(cat.Id, cat.Nom, cat.Description, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "ADMINISTRATEUR")]
    public async Task<IActionResult> Update(int id, UpdateCategorieRequest request)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return NotFound(new { message = "Category not found." });

        if (await _db.Categories.AnyAsync(c => c.Nom == request.Nom && c.Id != id))
            return BadRequest(new { message = "A category with this name already exists." });

        cat.Nom = request.Nom;
        cat.Description = request.Description;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(CurrentUserId(), "UPDATE_CATEGORY", "categorie", id);
        return Ok(new { message = "Category updated successfully." });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "ADMINISTRATEUR")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.Include(c => c.Documents).FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return NotFound(new { message = "Category not found." });

        if (cat.Documents.Count > 0)
            return BadRequest(new { message = $"Cannot delete: {cat.Documents.Count} document(s) use this category." });

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(CurrentUserId(), "DELETE_CATEGORY", "categorie", id);
        return Ok(new { message = "Category deleted successfully." });
    }

    private static string ExtractPdfText(Stream stream, int maxPages)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(stream);
        foreach (var page in pdf.GetPages().Take(maxPages))
        {
            // Word-by-word extraction preserves reading order better than page.Text
            foreach (var word in page.GetWords())
                sb.Append(word.Text).Append(' ');
            sb.Append('\n');
        }
        return sb.ToString().Trim();
    }
}
