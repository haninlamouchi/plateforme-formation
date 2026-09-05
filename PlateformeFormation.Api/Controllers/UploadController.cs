using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlateformeFormation.Api.Helpers;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxBytes = 3 * 1024 * 1024;

    public UploadController(IWebHostEnvironment env) => _env = env;

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file received." });

        if (file.Length > MaxBytes)
            return BadRequest(new { message = "Image too large (3 MB maximum)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Unsupported format. Use JPG, PNG, or WEBP." });

        if (!await FileValidationHelper.IsImageAsync(file, ext))
            return BadRequest(new { message = "File content does not match the declared image format." });

        var folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream);

        return Ok(new { url = $"/uploads/avatars/{fileName}" });
    }
}
