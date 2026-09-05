using System.Text.Json;
using PlateformeFormation.Api.Helpers;

namespace PlateformeFormation.Api.Tests;

public class FormationContentParserTests
{
    // The reported bug: Groq output occasionally uses U+2011 (non-breaking hyphen, "‑") instead of a
    // plain "-" in words like "pré‑production" — the PDF export's embedded Arial subset doesn't cover
    // that code point, rendering it as a blank box. GetString/GetStringArray normalize it to a plain
    // hyphen at ingestion so newly generated/corrected content never carries it through to export.
    [Fact]
    public void GetString_NormalizesNonBreakingHyphenToPlainHyphen()
    {
        using var doc = JsonDocument.Parse("""{"contexte":"Contrôle qualité pré‑production et non‑conformité"}""");
        var value = FormationContentParser.GetString(doc.RootElement, "contexte");

        Assert.Equal("Contrôle qualité pré-production et non-conformité", value);
        Assert.DoesNotContain('‑', value);
    }

    [Fact]
    public void GetStringArray_NormalizesNonBreakingHyphenToPlainHyphen()
    {
        using var doc = JsonDocument.Parse("""{"contenu":["Vérification pré‑production","Traitement des non‑conformités"]}""");
        var values = FormationContentParser.GetStringArray(doc.RootElement, "contenu");

        Assert.Equal(["Vérification pré-production", "Traitement des non-conformités"], values);
    }
}
