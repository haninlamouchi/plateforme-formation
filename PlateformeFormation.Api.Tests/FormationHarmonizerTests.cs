using System.Text.Json;
using PlateformeFormation.Api.Helpers;

namespace PlateformeFormation.Api.Tests;

public class FormationHarmonizerTests
{
    // Bug 1 (original schema) is structurally eliminated in the new schema — there is no more
    // LLM-generated day plan to harmonize. What remains provable: the formation total is always the
    // sum of real module durations.
    [Fact]
    public void HarmonizeDureeFromModules_SumsModuleDurations()
    {
        var modulesJson = """[{"numero":1,"dureeHeures":3},{"numero":2,"dureeHeures":4.5}]""";

        var result = FormationHarmonizer.HarmonizeDureeFromModules(modulesJson, null, null);

        Assert.Equal(7.5m, result);
    }

    [Fact]
    public void HarmonizeDureeFromModules_IncludesModuleBonusWhenInCoreTrack()
    {
        var modulesJson = """[{"numero":1,"dureeHeures":3}]""";
        var objectifsJson = """{"moduleBonus":{"inclusDansTroncCommun":true,"dureeHeures":2}}""";

        var result = FormationHarmonizer.HarmonizeDureeFromModules(modulesJson, objectifsJson, null);

        Assert.Equal(5m, result);
    }

    [Fact]
    public void RescaleEvaluationWeights_ForcesExactly100()
    {
        var json = """[{"nom":"A","pct":70},{"nom":"B","pct":45}]""";

        var result = FormationHarmonizer.RescaleEvaluationWeights(json);

        using var doc = JsonDocument.Parse(result);
        var sum = doc.RootElement.EnumerateArray().Sum(e => e.GetProperty("pct").GetDouble());
        Assert.Equal(100, sum, 0.01);
    }

    [Fact]
    public void RemoveCertificationEntries_StripsAttestationAndRebalances()
    {
        var json = """
            [
              {"nom":"Étude de cas","pct":40,"estEvaluationContinue":false},
              {"nom":"Évaluation continue","pct":40,"estEvaluationContinue":true},
              {"nom":"Attestation de compétences","pct":20,"estEvaluationContinue":false}
            ]
            """;

        var stripped = FormationHarmonizer.RemoveCertificationEntries(json);
        using (var doc = JsonDocument.Parse(stripped))
            Assert.Equal(2, doc.RootElement.GetArrayLength());

        var rebalanced = FormationHarmonizer.RescaleEvaluationWeights(stripped);
        using var doc2 = JsonDocument.Parse(rebalanced);
        var noms = doc2.RootElement.EnumerateArray().Select(e => e.GetProperty("nom").GetString()).ToList();
        var sum = doc2.RootElement.EnumerateArray().Sum(e => e.GetProperty("pct").GetDouble());

        Assert.DoesNotContain(noms, n => n!.Contains("Attestation"));
        Assert.Equal(100, sum, 0.01);
    }

    // Each module's "objectif" (singular, per-module in this schema) must be phrased as a measurable
    // outcome.
    [Fact]
    public void EnforceMeasurableObjectivePrefix_PrependsMissingPrefix()
    {
        var json = """[{"numero":1,"objectif":"Comprendre les bases de X"},{"numero":2,"objectif":"Être capable de faire Y"}]""";

        var result = FormationHarmonizer.EnforceMeasurableObjectivePrefix(json);

        using var doc = JsonDocument.Parse(result);
        var objectifs = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("objectif").GetString()).ToList();
        Assert.StartsWith("Être capable de", objectifs[0]);
        Assert.Equal("Être capable de faire Y", objectifs[1]); // already compliant, left untouched
    }

    // "Être capable de analyser..." is grammatically wrong — French elides "de" before a vowel or "h".
    [Fact]
    public void ApplyElisionToObjectifs_ElidesBeforeVowelOrH()
    {
        var json = """
            [
              {"numero":1,"objectif":"Être capable de analyser un jeu de données"},
              {"numero":2,"objectif":"Être capable de héberger une application"},
              {"numero":3,"objectif":"Être capable de concevoir une architecture"}
            ]
            """;

        var result = FormationHarmonizer.ApplyElisionToObjectifs(json);

        using var doc = JsonDocument.Parse(result);
        var objectifs = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("objectif").GetString()).ToList();
        Assert.Equal("Être capable d'analyser un jeu de données", objectifs[0]);
        Assert.Equal("Être capable d'héberger une application", objectifs[1]);
        Assert.Equal("Être capable de concevoir une architecture", objectifs[2]); // consonant verb, untouched
    }

    // Rules 3 & 4: a module can only depend on strictly-earlier modules. This is now mechanically
    // fixable (not just detectable) because the schema expresses dependencies as numero references.
    [Fact]
    public void SanitizeCompetencesPrerequises_StripsForwardReferences()
    {
        var json = """
            [
              {"numero":1,"competencesPrerequises":[]},
              {"numero":2,"competencesPrerequises":[1,3],"reutiliseLivrableModule":5},
              {"numero":3,"competencesPrerequises":[1,2]}
            ]
            """;

        var result = FormationHarmonizer.SanitizeCompetencesPrerequises(json);

        using var doc = JsonDocument.Parse(result);
        var module2 = doc.RootElement[1];
        var prereqs = module2.GetProperty("competencesPrerequises").EnumerateArray().Select(e => e.GetInt32()).ToList();
        Assert.Equal([1], prereqs); // the forward reference to 3 is stripped
        Assert.Equal(JsonValueKind.Null, module2.GetProperty("reutiliseLivrableModule").ValueKind); // forward ref to 5 nulled
    }
}
