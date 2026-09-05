using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlateformeFormation.Api.Helpers;

// Shared deserialization for the JSON blobs stored in Formation.Objectifs/Modules/MethodesEvaluation
// (Formation.Activites is unused by the current schema). Used by FormationExportService (PDF
// rendering), FormationQualityService (pedagogical checks) and FormationController (computed
// PlanningJours) so none of them drift into parsing the same data two different ways.
public static class FormationContentParser
{
    public static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Formation.Modules/MethodesEvaluation are stored as camelCase JSON. Anything that re-serializes
    // typed C# records (PascalCase properties) back into that column — e.g. attaching traceability
    // sources — must use this naming policy, otherwise the stored JSON silently flips to PascalCase
    // and the frontend's plain `JSON.parse` + lowercase field access breaks.
    public static readonly JsonSerializerOptions JsonWriteOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static readonly Regex PercentPattern = new(@"(\d+(?:[.,]\d+)?)\s*%", RegexOptions.Compiled);
    public static readonly Regex HoursPattern = new(@"(\d+(?:[.,]\d+)?)\s*h", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParseNumber(string text, Regex pattern, out double value)
    {
        value = 0;
        var m = pattern.Match(text ?? "");
        return m.Success && double.TryParse(
            m.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    // Tolerates older/incompatible-shaped data (pre-dating a schema change, or partially edited) by
    // returning null instead of throwing — callers fall back to sensible defaults. This is also what
    // lets a formation generated under the OLD schema still open without crashing: unknown JSON
    // properties are silently ignored and missing ones become null.
    public static T? ParseObject<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }

    public static List<T> ParseCards<T>(string? json, Func<T> fallback) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            if (json.TrimStart().StartsWith('['))
                return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? [];
        }
        catch (JsonException) { /* fall through */ }

        return [fallback()];
    }

    // Normalizes text (accents/case/punctuation-insensitive) for word-overlap comparisons — used to
    // detect near-duplicate livrables and to correlate a module's content with a test de
    // positionnement's vocabulary. Structural checks (module numero references) don't need this at
    // all anymore — that's the whole point of the numbered-reference schema.
    public static string NormalizeTitle(string s)
    {
        var formD = s.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var noAccents = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var alnumOnly = Regex.Replace(noAccents, @"[^a-z0-9]+", " ");
        return Regex.Replace(alnumOnly, @"\s+", " ").Trim();
    }

    // ---- Raw Groq JSON (snake_case, per the system prompt's schema) -> canonical camelCase stored
    // JSON. Shared by FormationGenerationService (initial generation) and FormationCorrectionService
    // (targeted re-write) so both produce identically-shaped output regardless of which Groq call
    // produced it.

    // Groq output occasionally uses U+2011 (non-breaking hyphen) instead of a plain hyphen in words
    // like "pré‑production" or "non‑conformité" — the PDF export's embedded Arial subset doesn't cover
    // that code point, so it rendered as a blank box. The typographic distinction (non-breaking) has no
    // visual meaning in this plain-text context, so it's normalized to a standard hyphen at ingestion
    // rather than chasing font coverage — every string field passing through here is covered, so this
    // can never happen again on newly generated or corrected content.
    private static string NormalizeHyphen(string s) => s.Replace('‑', '-');

    public static string GetString(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String ? NormalizeHyphen(v.GetString() ?? "") : "";

    public static List<string> GetStringArray(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => NormalizeHyphen(x.GetString() ?? "")).Where(s => s.Length > 0).ToList()
            : [];

    public static List<int> GetIntArray(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number)
                .Select(x => x.TryGetInt32(out var i) ? i : (int)x.GetDouble()).ToList()
            : [];

    public static double? GetDoubleOrNull(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public static int? GetIntOrNull(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Number
            ? (v.TryGetInt32(out var i) ? i : (int)v.GetDouble())
            : null;

    public static bool GetBool(JsonElement el, string field) =>
        el.TryGetProperty(field, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False && v.GetBoolean();

    // Everything that isn't "modules"/"evaluation"/"activites_pedagogiques" — sousTitre/contexte/
    // publicCible/prerequisDetailles/modalitesPratiques/beneficesEntreprise/sourcesUtilisees/
    // lacunesContexte/testPositionnement/moduleBonus/ressourcesPedagogiques/documentsSources —
    // stored together in Formation.Objectifs.
    public static string GetObjectifsJson(JsonElement root)
    {
        object? modalitesPratiques = null;
        if (root.TryGetProperty("modalites_pratiques", out var mpEl) && mpEl.ValueKind == JsonValueKind.Object)
        {
            modalitesPratiques = new
            {
                format = GetString(mpEl, "format"),
                intervenant = GetString(mpEl, "intervenant"),
                materielRequis = GetStringArray(mpEl, "materiel_requis"),
            };
        }

        var beneficesEntreprise = root.TryGetProperty("benefices_entreprise", out var beEl) && beEl.ValueKind == JsonValueKind.Array
            ? beEl.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
                .Select(e => new { titre = GetString(e, "titre"), justification = GetString(e, "justification") }).ToList()
            : [];

        object? testPositionnement = null;
        if (root.TryGetProperty("test_positionnement", out var tpEl) && tpEl.ValueKind == JsonValueKind.Object)
        {
            testPositionnement = new
            {
                objectif = GetString(tpEl, "objectif"),
                qcmQuestions = GetIntOrNull(tpEl, "qcm_questions"),
                exercice = GetString(tpEl, "exercice"),
                seuilParcoursStandardPct = GetDoubleOrNull(tpEl, "seuil_parcours_standard_pct"),
                moduleRemediation = GetIntOrNull(tpEl, "module_remediation"),
            };
        }

        object? moduleBonus = null;
        if (root.TryGetProperty("module_bonus", out var mbEl) && mbEl.ValueKind == JsonValueKind.Object)
        {
            moduleBonus = new
            {
                inclusDansTroncCommun = GetBool(mbEl, "inclus_dans_tronc_commun"),
                titre = GetString(mbEl, "titre"),
                dureeHeures = GetDoubleOrNull(mbEl, "duree_heures"),
                contenu = GetStringArray(mbEl, "contenu"),
            };
        }

        return JsonSerializer.Serialize(new
        {
            sousTitre = GetString(root, "sous_titre"),
            contexte = GetString(root, "contexte"),
            publicCible = GetString(root, "public_cible"),
            prerequisDetailles = GetString(root, "prerequis_detailles"),
            modalitesPratiques,
            beneficesEntreprise,
            sourcesUtilisees = GetStringArray(root, "sources_utilisees"),
            lacunesContexte = GetStringArray(root, "lacunes_contexte"),
            testPositionnement,
            moduleBonus,
            ressourcesPedagogiques = GetStringArray(root, "ressources_pedagogiques"),
            documentsSources = GetStringArray(root, "documents_sources"),
        });
    }

    // Single-module snake_case -> camelCase mapping, shared by GetModulesJson (whole-formation
    // generation/correction) and FormationModuleRegenerationService (single-module regenerate) so
    // both paths produce identically-shaped module JSON instead of maintaining two copies of this
    // mapping that could silently drift apart.
    public static object MapModule(JsonElement el)
    {
        object? methode = null;
        if (el.TryGetProperty("methode", out var mEl) && mEl.ValueKind == JsonValueKind.Object)
        {
            methode = new
            {
                type = GetString(mEl, "type"),
                pctTheorie = GetDoubleOrNull(mEl, "pct_theorie"),
                pctPratique = GetDoubleOrNull(mEl, "pct_pratique"),
            };
        }

        object? exerciceFormatif = null;
        if (el.TryGetProperty("exercice_formatif", out var exEl) && exEl.ValueKind == JsonValueKind.Object)
        {
            exerciceFormatif = new
            {
                type = GetString(exEl, "type"),
                consigne = GetString(exEl, "consigne"),
                criteresReussite = GetStringArray(exEl, "criteres_reussite"),
                materiel = GetString(exEl, "materiel"),
                dureeMin = GetIntOrNull(exEl, "duree_min"),
            };
        }

        var grilleEvaluation = el.TryGetProperty("grille_evaluation", out var geEl) && geEl.ValueKind == JsonValueKind.Array
            ? geEl.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
                .Select(e => new { critere = GetString(e, "critere"), pct = GetDoubleOrNull(e, "pct") ?? 0 }).ToList()
            : [];

        return new
        {
            numero = GetIntOrNull(el, "numero") ?? 0,
            titre = GetString(el, "titre"),
            dureeHeures = GetDoubleOrNull(el, "duree_heures"),
            objectif = GetString(el, "objectif"),
            methode,
            contenu = GetStringArray(el, "contenu"),
            exerciceFormatif,
            livrable = GetString(el, "livrable"),
            reutiliseLivrableModule = GetIntOrNull(el, "reutilise_livrable_module"),
            competencesPrerequises = GetIntArray(el, "competences_prerequises"),
            grilleEvaluation,
            notesFormateur = GetStringArray(el, "notes_formateur"),
        };
    }

    public static string GetModulesJson(JsonElement root)
    {
        if (!root.TryGetProperty("modules", out var value) || value.ValueKind != JsonValueKind.Array)
            return "[]";

        var items = value.EnumerateArray()
            .Where(el => el.ValueKind == JsonValueKind.Object)
            .Select(MapModule);

        return JsonSerializer.Serialize(items);
    }

    public static string GetEvaluationJson(JsonElement root)
    {
        if (!root.TryGetProperty("evaluation", out var value) || value.ValueKind != JsonValueKind.Array)
            return "[]";

        var items = value.EnumerateArray()
            .Where(el => el.ValueKind == JsonValueKind.Object)
            .Select(el => new
            {
                nom = GetString(el, "nom"),
                pct = GetDoubleOrNull(el, "pct") ?? 0,
                estEvaluationContinue = GetBool(el, "est_evaluation_continue"),
            });

        return JsonSerializer.Serialize(items);
    }

    // activites_pedagogiques -> Formation.Activites (revived by this schema; the previous v2 schema
    // had folded this concept away entirely).
    public static string GetActivitesJson(JsonElement root)
    {
        if (!root.TryGetProperty("activites_pedagogiques", out var value) || value.ValueKind != JsonValueKind.Array)
            return "[]";

        var items = value.EnumerateArray()
            .Where(el => el.ValueKind == JsonValueKind.Object)
            .Select(el => new
            {
                nom = GetString(el, "nom"),
                format = GetString(el, "format"),
                description = GetString(el, "description"),
            });

        return JsonSerializer.Serialize(items);
    }

    public static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start == -1 || end == -1 ? "" : text[start..(end + 1)];
    }
}
