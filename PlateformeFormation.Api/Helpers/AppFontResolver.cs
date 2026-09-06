using PdfSharpCore.Fonts;

namespace PlateformeFormation.Api.Helpers;

// PdfSharpCore's default resolver can't find "Arial" on Linux (it's a proprietary font, not present
// in the container) and its fallback picks a single font file regardless of the requested weight —
// which is why every line of an exported PDF rendered bold instead of only titles/labels. This
// resolver maps Regular/Bold/Italic/BoldItalic to whichever real font files actually exist on the
// running OS, so the four weights always resolve to four distinct, correct files.
public class AppFontResolver : IFontResolver
{
    public string DefaultFontName => "Regular";

    private static readonly Dictionary<string, string[]> FaceCandidates = new()
    {
        ["Regular"] = ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", @"C:\Windows\Fonts\arial.ttf"],
        ["Bold"] = ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", @"C:\Windows\Fonts\arialbd.ttf"],
        ["Italic"] = ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf", @"C:\Windows\Fonts\ariali.ttf"],
        ["BoldItalic"] = ["/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf", @"C:\Windows\Fonts\arialbi.ttf"],
    };

    public byte[] GetFont(string faceName)
    {
        var path = FaceCandidates[faceName].FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"No font file found for face '{faceName}'.");
        return File.ReadAllBytes(path);
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var face = (isBold, isItalic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            (false, false) => "Regular",
        };
        return new FontResolverInfo(face);
    }
}
