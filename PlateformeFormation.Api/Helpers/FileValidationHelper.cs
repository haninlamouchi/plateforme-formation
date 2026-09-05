namespace PlateformeFormation.Api.Helpers;

public static class FileValidationHelper
{
    public static async Task<bool> IsPdfAsync(IFormFile file)
    {
        var buffer = new byte[4];
        using var stream = file.OpenReadStream();
        if (await stream.ReadAsync(buffer) < 4) return false;
        // %PDF magic bytes: 25 50 44 46
        return buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46;
    }

    public static async Task<bool> IsImageAsync(IFormFile file, string extension)
    {
        var buffer = new byte[12];
        using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(buffer);
        if (read < 4) return false;

        return extension switch
        {
            ".jpg" or ".jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,
            ".png" => buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47,
            ".webp" => read >= 12
                       && buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46
                       && buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50,
            _ => false
        };
    }
}
