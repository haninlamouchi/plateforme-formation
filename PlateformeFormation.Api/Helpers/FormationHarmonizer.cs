using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PlateformeFormation.Api.Models;
using static PlateformeFormation.Api.Helpers.FormationContentParser;

namespace PlateformeFormation.Api.Helpers;

// Deterministic, code-level fixes for arithmetic and structural rules real testing showed the model
// can't reliably self-check. Used both right after generation (FormationGenerationService) and on
// demand when correcting an existing formation (FormationCorrectionService) — same logic either way.
//
// The numbered-module schema (ModuleCard.Numero, CompetencesPrerequises, ReutiliseLivrableModule)
// turns what used to be lexical-overlap heuristics into pure integer comparisons: a forward reference
// is either present or it isn't, no guessing required. That's what makes SanitizeCompetencesPrerequises
// possible — a purely mechanical fix with no LLM call, for rules that previously could only be
// detected, never repaired, in code.
public static class FormationHarmonizer
{
    // Evaluation weightings must total 100%. Rescale whichever entries carry a numeric pct so they
    // provably sum to 100 — the last entry absorbs the rounding remainder.
    public static string RescaleEvaluationWeights(string evaluationJson)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<EvaluationItem>>(evaluationJson, JsonOpts) ?? [];
            if (items.Count < 2) return evaluationJson;

            var sum = items.Sum(e => e.Pct ?? 0);
            if (Math.Abs(sum - 100) < 0.5) return evaluationJson;
            if (sum <= 0) return evaluationJson;

            var scale = 100.0 / sum;
            var runningTotal = 0;
            var rescaled = new List<EvaluationItem>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var original = items[i].Pct ?? 0;
                double newVal;
                if (i == items.Count - 1) newVal = 100 - runningTotal;
                else { newVal = Math.Round(original * scale); runningTotal += (int)newVal; }
                rescaled.Add(items[i] with { Pct = newVal });
            }

            return JsonSerializer.Serialize(rescaled, JsonWriteOpts);
        }
        catch (JsonException)
        {
            return evaluationJson;
        }
    }

    // Certification/attestation is a completion artifact, never a graded item. Removed mechanically
    // rather than trusted to prompt compliance alone. Always run BEFORE RescaleEvaluationWeights so
    // the remaining entries rebalance to 100% without the removed entry's share.
    private static readonly string[] CertificationKeywords = ["attestation", "certificat", "certification"];

    public static string RemoveCertificationEntries(string evaluationJson)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<EvaluationItem>>(evaluationJson, JsonOpts) ?? [];
            var filtered = items.Where(it => !CertificationKeywords.Any(k =>
                (it.Nom ?? "").Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();

            return filtered.Count == items.Count ? evaluationJson : JsonSerializer.Serialize(filtered, JsonWriteOpts);
        }
        catch (JsonException)
        {
            return evaluationJson;
        }
    }

    // The per-module durations (numeric, no regex parsing needed with this schema) are the most
    // granular, reliable source of truth — the top-level total is recomputed from their sum, plus the
    // module bonus's hours when it's actually part of the core track.
    public static decimal? HarmonizeDureeFromModules(string modulesJson, string? objectifsJson, decimal? fallback)
    {
        try
        {
            var modules = JsonSerializer.Deserialize<List<ModuleCard>>(modulesJson, JsonOpts) ?? [];
            if (modules.Count == 0 || modules.Any(m => m.DureeHeures is null)) return fallback;

            var total = modules.Sum(m => m.DureeHeures!.Value);

            var objectifs = ParseObject<ObjectifsData>(objectifsJson);
            if (objectifs?.ModuleBonus is { InclusDansTroncCommun: true, DureeHeures: not null } bonus)
                total += bonus.DureeHeures!.Value;

            return (decimal)Math.Round(total, 1);
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    // Every module's objectif must be phrased as a measurable outcome. The required verb ITSELF
    // (e.g. swapping a banned "comprendre" for a real observable verb) needs understanding of intent
    // and stays FormationQualityService's job — but prepending the missing sentence starter is a
    // purely mechanical fix that never needs to guess meaning. Never doubles an existing prefix.
    // "etre capable d" (no trailing space) matches both the plain "de " form and the elided "d'" form
    // ApplyElisionToObjectifs can produce — otherwise a later harmonizer pass on an already-elided
    // objectif would fail to recognize the prefix and double it.
    private static readonly string[] RequiredObjectivePrefixes = ["etre capable d", "savoir"];

    public static string EnforceMeasurableObjectivePrefix(string modulesJson)
    {
        try
        {
            if (JsonNode.Parse(modulesJson) is not JsonArray modules) return modulesJson;

            foreach (var moduleNode in modules)
            {
                if (moduleNode is not JsonObject module) continue;
                var text = module["objectif"]?.GetValue<string>() ?? "";
                if (text.Length == 0) continue;
                if (RequiredObjectivePrefixes.Any(p => NormalizeTitle(text).StartsWith(p))) continue;

                module["objectif"] = $"Être capable de {char.ToLowerInvariant(text[0])}{text[1..]}";
            }

            return modules.ToJsonString();
        }
        catch (JsonException)
        {
            return modulesJson;
        }
    }

    // "Être capable de analyser..." is grammatically wrong — French elides "de" to "d'" before a verb
    // starting with a vowel or (conventionally, for this simple case) "h". Purely textual, deterministic,
    // no LLM call needed. Scoped to the fixed "Être capable de <verbe>" construction only — "de" isn't
    // elided everywhere in French, just in this specific sentence starter.
    private static readonly Regex ElisionRegex = new(@"^Être capable de (?=[aeiouhAEIOUH])", RegexOptions.Compiled);

    public static string ApplyElisionToObjectifs(string modulesJson)
    {
        try
        {
            if (JsonNode.Parse(modulesJson) is not JsonArray modules) return modulesJson;

            foreach (var moduleNode in modules)
            {
                if (moduleNode is not JsonObject module) continue;
                var text = module["objectif"]?.GetValue<string>();
                if (string.IsNullOrEmpty(text)) continue;

                module["objectif"] = ElisionRegex.Replace(text, "Être capable d'");
            }

            return modules.ToJsonString();
        }
        catch (JsonException)
        {
            return modulesJson;
        }
    }

    // Rules 3 & 4 (progression + livrable chaining): a module can only depend on modules that come
    // strictly before it. Any forward or self reference is a guaranteed authoring mistake (the schema
    // makes this provable, unlike the old vocabulary-overlap heuristics) — stripped mechanically, no
    // LLM call needed.
    public static string SanitizeCompetencesPrerequises(string modulesJson)
    {
        try
        {
            if (JsonNode.Parse(modulesJson) is not JsonArray modules) return modulesJson;

            foreach (var moduleNode in modules)
            {
                if (moduleNode is not JsonObject module) continue;
                var numero = module["numero"]?.GetValue<int>() ?? 0;

                if (module["competencesPrerequises"] is JsonArray prereqs)
                {
                    var valid = prereqs
                        .Where(p => p is not null && p.GetValue<int>() < numero)
                        .Select(p => p!.GetValue<int>())
                        .ToList();
                    module["competencesPrerequises"] = new JsonArray(valid.Select(v => (JsonNode)v).ToArray());
                }

                var reuse = module["reutiliseLivrableModule"];
                if (reuse is not null && reuse.GetValue<int>() >= numero)
                    module["reutiliseLivrableModule"] = null;
            }

            return modules.ToJsonString();
        }
        catch (JsonException)
        {
            return modulesJson;
        }
    }
}
