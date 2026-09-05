using System.Text;
using UglyToad.PdfPig;

namespace PlateformeFormation.Api.Helpers;

public static class PdfTextExtractor
{
    public record ExtractedPdfPage(int Number, string Text);

    public static string ExtractFullText(Stream pdfStream)
    {
        return string.Join('\n', ExtractPages(pdfStream).Select(page => page.Text)).Trim();
    }

    // Page-level extraction makes every indexed passage auditable in the original PDF.
    public static List<ExtractedPdfPage> ExtractPages(Stream pdfStream)
    {
        var pages = new List<ExtractedPdfPage>();
        using var pdf = PdfDocument.Open(pdfStream);
        foreach (var page in pdf.GetPages())
        {
            var sb = new StringBuilder();
            // Word-by-word extraction preserves reading order better than page.Text
            foreach (var word in page.GetWords())
                sb.Append(word.Text).Append(' ');
            var text = sb.ToString().Trim();
            if (text.Length > 0)
                pages.Add(new ExtractedPdfPage(page.Number, text));
        }
        return pages;
    }
}
