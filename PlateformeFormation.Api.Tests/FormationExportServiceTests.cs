using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Tests;

public class FormationExportServiceTests
{
    // Simulates a formation already stored in the DB with U+2011 (non-breaking hyphen) baked into its
    // content — content generated before FormationContentParser started normalizing it at ingestion.
    // The render-time SanitizeText() fix in FormationExportService is what has to catch this case;
    // this is a smoke test (GeneratePdf must not throw and must produce a non-empty PDF) since
    // PdfSharpCore doesn't expose a way to introspect which glyph actually got drawn.
    [Fact]
    public void GeneratePdf_HandlesAlreadyStoredNonBreakingHyphenWithoutThrowing()
    {
        var formation = new Formation
        {
            Id = 1,
            Titre = "Formation qualité",
            CreePar = 1,
            Objectifs = """{"contexte":"Contrôle qualité pré‑production et gestion des non‑conformités"}""",
            Modules = """
                [
                  {"numero":1,"titre":"Contrôles pré‑production","dureeHeures":2,
                   "objectif":"Savoir détecter une non‑conformité avant mise en production",
                   "contenu":["Vérification pré‑production","Traitement des non‑conformités"],
                   "livrable":"Rapport de non‑conformité"}
                ]
                """,
            Activites = "[]",
            MethodesEvaluation = """[{"nom":"Contrôle pré‑production","pct":100,"estEvaluationContinue":false}]""",
        };

        var service = new FormationExportService(new FormationQualityService());
        var bytes = service.GeneratePdf(formation);

        Assert.NotEmpty(bytes);
    }
}
