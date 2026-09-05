using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;
using static PlateformeFormation.Api.Helpers.FormationContentParser;

namespace PlateformeFormation.Api.Services;

// Turns the pedagogical rules (R1-R13, 2026-08 spec) the generation prompt only ASKS the model to
// follow into checks that are actually VERIFIED. The numbered-module schema (ModuleCard.Numero,
// CompetencesPrerequises, ReutiliseLivrableModule) turns what used to be lexical-overlap heuristics
// into structural integer comparisons.
//
// Pure and synchronous: no LLM call, no DB query. Computed fresh on every read so it reflects the
// CURRENT state of a formation, including after manual edits. `FormationQualityReport.EstValide` is
// the strict blocking view of these same checks — see IFormationQualityService.cs.
public class FormationQualityService : IFormationQualityService
{
    private const string Ok = "OK";
    private const string Warn = "AVERTISSEMENT";
    private const string Fail = "ECHEC";

    private static readonly Dictionary<string, int> Weights = new()
    {
        ["PONDERATION_100"] = 2,          // R5
        ["EVALUATION_CONTINUE"] = 2,      // R6
        ["HEURES_MODULES"] = 2,           // R1
        ["LIVRABLE_UNIQUE"] = 2,          // R4
        ["ATTESTATION_NON_PONDEREE"] = 2, // R7
        ["LIVRABLE_PRESENT"] = 1,
        ["LIVRABLE_CHAINE"] = 1,
        ["EXERCICE_FORMATIF"] = 1,
        ["EXERCICE_DETAILLE"] = 1,        // R12
        ["PROGRESSION_COMPETENCES"] = 1,  // R3
        ["OBJECTIFS_MESURABLES"] = 1,     // R2
        ["CONTENU_DETAILLE"] = 1,         // R11
        ["RATIO_THEORIE_PRATIQUE"] = 1,
        ["RESSOURCES"] = 1,               // R10
        ["REMEDIATION_CIBLEE"] = 1,       // R8
        ["TEST_POSITIONNEMENT"] = 1,
        ["MODULE_BONUS_COHERENT"] = 1,    // R9
    };

    private static readonly string[] BannedVerbs = ["comprendre", "découvrir", "connaître", "connaitre", "apprendre"];
    private static readonly string[] CertificationKeywords = ["attestation", "certificat", "certification"];

    private static readonly HashSet<string> StopWords =
        ["le", "la", "les", "des", "du", "un", "une", "avec", "pour", "dans", "sur", "aux", "et", "de", "en", "au", "ce", "cette"];

    public FormationQualityReport Evaluate(Formation formation)
    {
        var objectifs = ParseObject<ObjectifsData>(formation.Objectifs) ?? new ObjectifsData(
            null, null, null, null, null, null, null, null, null, null, null, null);
        var modules = ParseCards<ModuleCard>(formation.Modules, () => new ModuleCard(
            0, formation.Modules, null, null, null, null, null, null, null, null, null, null, null));
        var evaluation = ParseCards<EvaluationItem>(formation.MethodesEvaluation, () => new EvaluationItem(
            formation.MethodesEvaluation, null, false));

        var hasModules = modules.Count > 0 && modules.Any(m => m.DureeHeures.HasValue || m.Contenu is { Count: > 0 });
        var hasEvaluation = evaluation.Count > 0 && evaluation.Any(e => e.Pct is not null);

        var checks = new List<QualityCheck>();

        CheckPonderation100(evaluation, hasEvaluation, checks);
        CheckEvaluationContinue(evaluation, hasEvaluation, checks);
        CheckHeuresModules(formation, modules, objectifs, hasModules, checks);
        CheckLivrablePresent(modules, hasModules, checks);
        CheckLivrableUnique(modules, hasModules, checks);
        CheckLivrableChaine(modules, hasModules, checks);
        CheckExerciceFormatif(modules, hasModules, checks);
        CheckExerciceDetaille(modules, hasModules, checks);
        CheckProgressionCompetences(modules, hasModules, checks);
        CheckObjectifsMesurables(modules, hasModules, checks);
        CheckContenuDetaille(modules, hasModules, checks);
        CheckRatioTheoriePratique(modules, hasModules, checks);
        CheckRessources(objectifs, checks);
        CheckRemediationCiblee(objectifs, modules, hasModules, checks);
        CheckTestPositionnement(objectifs, checks);
        CheckAttestationNonPonderee(evaluation, hasEvaluation, checks);
        CheckModuleBonusCoherent(objectifs, evaluation, checks);

        var score = ComputeScore(checks);
        var niveau = score >= 85 ? "EXCELLENT" : score >= 60 ? "BON" : "A_REVOIR";

        return new FormationQualityReport(score, niveau, checks);
    }

    private static int ComputeScore(List<QualityCheck> checks)
    {
        double earned = 0, total = 0;
        foreach (var c in checks)
        {
            var w = Weights.GetValueOrDefault(c.Code, 1);
            total += w;
            earned += c.Statut switch { Ok => w, Warn => w * 0.5, _ => 0 };
        }
        return total <= 0 ? 0 : (int)Math.Round(100 * earned / total);
    }

    // R5
    private static void CheckPonderation100(List<EvaluationItem> evaluation, bool hasEvaluation, List<QualityCheck> checks)
    {
        const string code = "PONDERATION_100";
        const string libelle = "Les pondérations d'évaluation totalisent 100 %";
        if (!hasEvaluation) { checks.Add(new(code, libelle, Warn, "Aucune méthode d'évaluation structurée.")); return; }

        var withPct = evaluation.Where(e => e.Pct is not null).ToList();
        if (withPct.Count < 2)
        {
            checks.Add(new(code, libelle, Warn, "Pas assez de pondérations chiffrées pour vérifier le total."));
            return;
        }

        var sum = withPct.Sum(e => e.Pct!.Value);
        checks.Add(Math.Abs(sum - 100) < 0.5
            ? new(code, libelle, Ok, $"Total observé : {sum:0.#} %.")
            : new(code, libelle, Fail, $"Total observé : {sum:0.#} % (attendu 100 %)."));
    }

    // R6
    private static void CheckEvaluationContinue(List<EvaluationItem> evaluation, bool hasEvaluation, List<QualityCheck> checks)
    {
        const string code = "EVALUATION_CONTINUE";
        const string libelle = "Une évaluation continue pondérée 20-30 % est présente";
        if (!hasEvaluation) { checks.Add(new(code, libelle, Warn, "Aucune méthode d'évaluation structurée.")); return; }

        var candidate = evaluation.FirstOrDefault(e => e.EstEvaluationContinue);
        if (candidate is null) { checks.Add(new(code, libelle, Fail, "Aucune entrée marquée évaluation continue.")); return; }

        if (candidate.Pct is { } v && v >= 20 && v <= 30)
            checks.Add(new(code, libelle, Ok, $"« {candidate.Nom} » pondérée {v:0.#} %."));
        else
            checks.Add(new(code, libelle, Warn, $"« {candidate.Nom} » trouvée mais pondération hors 20-30 % ou non chiffrée."));
    }

    // R1: module_bonus counts toward the total only when it's part of the core track.
    private static void CheckHeuresModules(
        Formation formation, List<ModuleCard> modules, ObjectifsData objectifs, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "HEURES_MODULES";
        const string libelle = "La somme des durées de modules (+ module bonus le cas échéant) correspond à la durée totale";
        if (!hasModules || formation.DureeEstimee is null)
        {
            checks.Add(new(code, libelle, Warn, "Données insuffisantes pour vérifier."));
            return;
        }
        if (modules.Any(m => m.DureeHeures is null))
        {
            checks.Add(new(code, libelle, Warn, "Certains modules n'ont pas de durée exploitable."));
            return;
        }

        var sum = modules.Sum(m => m.DureeHeures!.Value);
        if (objectifs.ModuleBonus is { InclusDansTroncCommun: true, DureeHeures: not null } bonus)
            sum += bonus.DureeHeures!.Value;

        var target = (double)formation.DureeEstimee.Value;
        checks.Add(Math.Abs(sum - target) < 0.15
            ? new(code, libelle, Ok, $"{sum:0.#} h sur {target:0.#} h.")
            : new(code, libelle, Fail, $"Modules : {sum:0.#} h, durée totale affichée : {target:0.#} h."));
    }

    private static void CheckLivrablePresent(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "LIVRABLE_PRESENT";
        const string libelle = "Chaque module a un livrable défini";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var missing = modules.Count(m => string.IsNullOrWhiteSpace(m.Livrable));
        checks.Add(missing == 0
            ? new(code, libelle, Ok, $"{modules.Count} module(s), tous pourvus d'un livrable.")
            : new(code, libelle, Fail, $"{missing} module(s) sans livrable sur {modules.Count}."));
    }

    // R4: >85% word-overlap similarity, checked pairwise across ALL modules regardless of chaining —
    // a chained livrable (reutiliseLivrableModule set) must still describe an evolution, not a repeat.
    private static void CheckLivrableUnique(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "LIVRABLE_UNIQUE";
        const string libelle = "Aucun livrable dupliqué ou quasi-identique (> 85 %) entre modules";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var withLivrable = modules.Where(m => !string.IsNullOrWhiteSpace(m.Livrable)).ToList();
        var duplicates = new List<string>();
        for (var i = 0; i < withLivrable.Count; i++)
        {
            for (var j = i + 1; j < withLivrable.Count; j++)
            {
                var wi = MeaningfulWords(withLivrable[i].Livrable!);
                var wj = MeaningfulWords(withLivrable[j].Livrable!);
                if (wi.Count == 0 || wj.Count == 0) continue;
                var overlap = wi.Intersect(wj).Count() / (double)Math.Max(wi.Count, wj.Count);
                if (overlap > 0.85)
                    duplicates.Add($"« {withLivrable[i].Titre} » et « {withLivrable[j].Titre} »");
            }
        }

        checks.Add(duplicates.Count == 0
            ? new(code, libelle, Ok, "Tous les livrables sont suffisamment distincts.")
            : new(code, libelle, Fail, $"Livrables trop proches entre : {string.Join(", ", duplicates)}."));
    }

    // Structural, not lexical: reutiliseLivrableModule must simply be set from the 2nd module onward.
    // Missing (null) can't be mechanically fixed — the correction pass has to decide which prior
    // module it should chain from — so this stays a content-judgment check.
    private static void CheckLivrableChaine(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "LIVRABLE_CHAINE";
        const string libelle = "Le livrable de chaque module (à partir du 2e) référence explicitement celui d'un module antérieur";
        if (!hasModules || modules.Count < 2) { checks.Add(new(code, libelle, Warn, "Pas assez de modules pour vérifier.")); return; }

        var ordered = modules.OrderBy(m => m.Numero).ToList();
        var missing = ordered.Skip(1)
            .Where(m => m.ReutiliseLivrableModule is null)
            .Select(m => m.Titre ?? $"module {m.Numero}")
            .ToList();

        checks.Add(missing.Count == 0
            ? new(code, libelle, Ok, "Chaînage renseigné sur tous les modules à partir du 2e.")
            : new(code, libelle, Fail, $"reutilise_livrable_module non renseigné pour : {string.Join(", ", missing)}."));
    }

    private static void CheckExerciceFormatif(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "EXERCICE_FORMATIF";
        const string libelle = "Chaque module a un exercice formatif distinct de son livrable";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var offenders = new List<string>();
        foreach (var m in modules)
        {
            var consigne = m.ExerciceFormatif?.Consigne;
            if (string.IsNullOrWhiteSpace(consigne)) { offenders.Add($"{m.Titre} (aucun)"); continue; }
            if (!string.IsNullOrWhiteSpace(m.Livrable) && NormalizeTitle(consigne) == NormalizeTitle(m.Livrable))
                offenders.Add($"{m.Titre} (identique au livrable)");
        }

        checks.Add(offenders.Count == 0
            ? new(code, libelle, Ok, "Exercice formatif présent et distinct sur chaque module.")
            : new(code, libelle, Fail, string.Join(", ", offenders)));
    }

    // R12: a real actionable exercise, not a one-line placeholder — consigne must be substantial and
    // success criteria must actually enumerate something to check against.
    private static void CheckExerciceDetaille(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "EXERCICE_DETAILLE";
        const string libelle = "Chaque exercice formatif a une consigne actionnable (> 15 mots) et au moins 2 critères de réussite";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var offenders = new List<string>();
        foreach (var m in modules)
        {
            var ex = m.ExerciceFormatif;
            if (ex is null || string.IsNullOrWhiteSpace(ex.Consigne)) continue; // covered by EXERCICE_FORMATIF
            var wordCount = ex.Consigne.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var criteresCount = ex.CriteresReussite?.Count ?? 0;
            if (wordCount <= 15 || criteresCount < 2)
                offenders.Add($"{m.Titre} ({wordCount} mot(s), {criteresCount} critère(s))");
        }

        checks.Add(offenders.Count == 0
            ? new(code, libelle, Ok, "Consignes détaillées et critères de réussite présents.")
            : new(code, libelle, Fail, $"Insuffisant sur : {string.Join(", ", offenders)}."));
    }

    // Vocabulary a module actually teaches — titre/objectif/contenu. Shared with CheckRemediationCiblee.
    private static HashSet<string> ModuleVocabulary(ModuleCard m)
    {
        var words = new HashSet<string>(MeaningfulWords(m.Titre ?? ""));
        words.UnionWith(MeaningfulWords(m.Objectif ?? ""));
        foreach (var c in m.Contenu ?? []) words.UnionWith(MeaningfulWords(c));
        return words;
    }

    private static HashSet<string> MeaningfulWords(string text) =>
        NormalizeTitle(text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && !StopWords.Contains(w))
            .ToHashSet();

    // R3: fully structural — a forward or self reference is a guaranteed authoring mistake. Also
    // mechanically fixable (FormationHarmonizer.SanitizeCompetencesPrerequises), so this is a
    // DeterministicCodes entry in FormationCorrectionService.
    private static void CheckProgressionCompetences(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "PROGRESSION_COMPETENCES";
        const string libelle = "Chaque module ne dépend que de modules numérotés strictement inférieurs";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var offenders = new List<string>();
        foreach (var m in modules)
        {
            var bad = (m.CompetencesPrerequises ?? []).Where(p => p >= m.Numero).ToList();
            if (bad.Count > 0)
                offenders.Add($"Module {m.Numero} (« {m.Titre} ») référence le(s) module(s) {string.Join(", ", bad)}");
            if (m.ReutiliseLivrableModule is { } reuse && reuse >= m.Numero)
                offenders.Add($"Module {m.Numero} : reutilise_livrable_module ({reuse}) n'est pas antérieur");
        }

        checks.Add(offenders.Count == 0
            ? new(code, libelle, Ok, "Aucune référence en avant détectée.")
            : new(code, libelle, Fail, string.Join(" ; ", offenders)));
    }

    // R2: required prefix present exactly once (never duplicated, e.g. "Être capable de être capable
    // de..." — a real bug seen in testing), plus absence of non-observable verbs.
    // Returns how many leading words form a valid "Être capable de"/"Être capable d'"/"Savoir" prefix
    // (3, 3, or 1 respectively), 0 if none match. Both "de" and the elided "d'" (normalized to a bare
    // "d" word, since NormalizeTitle turns the apostrophe into a space) count as valid.
    private static int CountObjectivePrefixWords(string[] words)
    {
        if (words.Length >= 1 && words[0] == "savoir") return 1;
        if (words.Length >= 3 && words[0] == "etre" && words[1] == "capable" && (words[2] == "de" || words[2] == "d"))
            return 3;
        return 0;
    }

    private static void CheckObjectifsMesurables(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "OBJECTIFS_MESURABLES";
        const string libelle = "Chaque objectif de module commence UNE SEULE FOIS par « Être capable de »/« Savoir » et utilise un verbe observable";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var missingPrefix = new List<string>();
        var duplicatedPrefix = new List<string>();
        var bannedVerb = new List<string>();
        foreach (var m in modules)
        {
            var obj = m.Objectif ?? "";
            if (obj.Length == 0) continue;
            // Word-aware, not character-sliced: NormalizeTitle turns "d'analyser" into "d analyser"
            // (apostrophe -> space), so "de" and the elided "d" form end up as DIFFERENT lengths after
            // normalization — a fixed-offset slice would misalign on one of the two forms.
            var words = NormalizeTitle(obj).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var prefixWordCount = CountObjectivePrefixWords(words);
            if (prefixWordCount == 0)
                missingPrefix.Add(m.Titre ?? $"module {m.Numero}");
            else if (CountObjectivePrefixWords(words.Skip(prefixWordCount).ToArray()) > 0)
                duplicatedPrefix.Add(m.Titre ?? $"module {m.Numero}");
            if (BannedVerbs.Any(v => obj.Contains(v, StringComparison.OrdinalIgnoreCase)))
                bannedVerb.Add(m.Titre ?? $"module {m.Numero}");
        }

        if (missingPrefix.Count == 0 && duplicatedPrefix.Count == 0 && bannedVerb.Count == 0)
        {
            checks.Add(new(code, libelle, Ok, $"{modules.Count} module(s), tous mesurables."));
            return;
        }

        var details = new List<string>();
        if (missingPrefix.Count > 0) details.Add($"sans préfixe requis : {string.Join(", ", missingPrefix)}");
        if (duplicatedPrefix.Count > 0) details.Add($"préfixe dupliqué : {string.Join(", ", duplicatedPrefix)}");
        if (bannedVerb.Count > 0) details.Add($"verbe non observable : {string.Join(", ", bannedVerb)}");
        checks.Add(new(code, libelle, Fail, string.Join(" ; ", details) + "."));
    }

    // R11: rejects keyword-list content — each item must be a real sentence (> 8 words), and there
    // must be at least 4 of them, so a module actually explains what's taught and why it matters.
    private static void CheckContenuDetaille(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "CONTENU_DETAILLE";
        const string libelle = "Chaque module a au moins 4 points de contenu, chacun une phrase complète (> 8 mots)";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var offenders = new List<string>();
        foreach (var m in modules)
        {
            var contenu = m.Contenu ?? [];
            var shortItems = contenu.Count(c => c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 8);
            if (contenu.Count < 4 || shortItems > 0)
                offenders.Add($"{m.Titre} ({contenu.Count} point(s), {shortItems} trop court(s))");
        }

        checks.Add(offenders.Count == 0
            ? new(code, libelle, Ok, "Contenu suffisamment détaillé sur tous les modules.")
            : new(code, libelle, Fail, $"Insuffisant sur : {string.Join(", ", offenders)}."));
    }

    private static void CheckRatioTheoriePratique(List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "RATIO_THEORIE_PRATIQUE";
        const string libelle = "Méthode pédagogique et ratio théorie/pratique précisés par module";
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés.")); return; }

        var missing = modules.Count(m =>
            string.IsNullOrWhiteSpace(m.Methode?.Type) || m.Methode?.PctTheorie is null || m.Methode?.PctPratique is null);
        checks.Add(missing == 0
            ? new(code, libelle, Ok, "Renseignés sur tous les modules.")
            : new(code, libelle, Warn, $"Manquant sur {missing} module(s) sur {modules.Count}."));
    }

    // R10: at least 3 resources AND none of them just repeats a source document's name — a resource
    // list that's identical to the sources isn't really a distinct pedagogical offering.
    private static void CheckRessources(ObjectifsData objectifs, List<QualityCheck> checks)
    {
        const string code = "RESSOURCES";
        const string libelle = "Au moins 3 ressources pédagogiques, distinctes des sources documentaires";
        var ressources = objectifs.RessourcesPedagogiques ?? [];
        var count = ressources.Count;
        if (count < 3)
        {
            checks.Add(new(code, libelle, Fail, $"{count} ressource(s) listée(s), 3 attendues au minimum."));
            return;
        }

        var sourceNames = (objectifs.SourcesUtilisees ?? []).Concat(objectifs.DocumentsSources ?? [])
            .Select(NormalizeTitle).ToHashSet();
        var duplicated = ressources.Where(r => sourceNames.Contains(NormalizeTitle(r))).ToList();

        checks.Add(duplicated.Count == 0
            ? new(code, libelle, Ok, $"{count} ressource(s) listée(s), toutes distinctes des sources.")
            : new(code, libelle, Fail, $"Identique(s) à une source : {string.Join(", ", duplicated)}."));
    }

    // R8: module_remediation is a module numero now, not free text — existence is a hard structural
    // check; whether it's the RIGHT module for what the test evaluates stays a soft (AVERTISSEMENT)
    // vocabulary-overlap signal — zero lexical overlap doesn't prove a mismatch given how much
    // professional prose paraphrases the same skill differently.
    private static void CheckRemediationCiblee(ObjectifsData objectifs, List<ModuleCard> modules, bool hasModules, List<QualityCheck> checks)
    {
        const string code = "REMEDIATION_CIBLEE";
        const string libelle = "Le module de remédiation correspond à la compétence testée";
        var test = objectifs.TestPositionnement;
        var moduleNum = test?.ModuleRemediation;
        if (moduleNum is null) { checks.Add(new(code, libelle, Fail, "Aucun module_remediation défini.")); return; }
        if (!hasModules) { checks.Add(new(code, libelle, Warn, "Pas de modules structurés pour vérifier.")); return; }

        var target = modules.FirstOrDefault(m => m.Numero == moduleNum);
        if (target is null)
        {
            checks.Add(new(code, libelle, Fail, $"module_remediation ({moduleNum}) ne correspond à aucun module existant."));
            return;
        }

        var testText = string.Join(" ", new[] { test?.Objectif, test?.Exercice }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (testText.Length == 0)
        {
            checks.Add(new(code, libelle, Ok, $"Référence le module {moduleNum} (« {target.Titre} »), pas assez de détail pour vérifier la correspondance."));
            return;
        }

        var matches = MeaningfulWords(testText).Overlaps(ModuleVocabulary(target));
        checks.Add(matches
            ? new(code, libelle, Ok, $"Le module {moduleNum} (« {target.Titre} ») correspond au contenu testé.")
            : new(code, libelle, Warn, $"Le module {moduleNum} (« {target.Titre} ») est référencé mais aucun mot-clé commun avec le test — à vérifier manuellement."));
    }

    private static void CheckTestPositionnement(ObjectifsData objectifs, List<QualityCheck> checks)
    {
        const string code = "TEST_POSITIONNEMENT";
        const string libelle = "Test de positionnement complet (objectif, exercice, seuil, remédiation)";
        var test = objectifs.TestPositionnement;
        var complet = test is not null
            && !string.IsNullOrWhiteSpace(test.Objectif)
            && !string.IsNullOrWhiteSpace(test.Exercice)
            && test.SeuilParcoursStandardPct is not null
            && test.ModuleRemediation is not null;

        checks.Add(complet
            ? new(code, libelle, Ok, "Présent et complet.")
            : new(code, libelle, Fail, "Absent ou incomplet."));
    }

    // R7
    private static void CheckAttestationNonPonderee(List<EvaluationItem> evaluation, bool hasEvaluation, List<QualityCheck> checks)
    {
        const string code = "ATTESTATION_NON_PONDEREE";
        const string libelle = "Aucune attestation/certification parmi les éléments notés";
        if (!hasEvaluation) { checks.Add(new(code, libelle, Warn, "Aucune méthode d'évaluation structurée.")); return; }

        var offenders = evaluation
            .Where(e => CertificationKeywords.Any(k => (e.Nom ?? "").Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.Nom ?? "")
            .ToList();

        checks.Add(offenders.Count == 0
            ? new(code, libelle, Ok, "Aucune attestation trouvée parmi les éléments pondérés.")
            : new(code, libelle, Fail, $"Présente comme élément noté : « {string.Join(" », « ", offenders)} »."));
    }

    // R9's evaluation half. The planning half is satisfied by construction (FormationPlanner always
    // includes an in-core-track module bonus), except when it has no duree — that case is caught here
    // too, since a bonus without hours could never appear in a computed plan either.
    private static void CheckModuleBonusCoherent(ObjectifsData objectifs, List<EvaluationItem> evaluation, List<QualityCheck> checks)
    {
        const string code = "MODULE_BONUS_COHERENT";
        const string libelle = "Le module bonus inclus au tronc commun a une durée et apparaît dans l'évaluation";
        var bonus = objectifs.ModuleBonus;
        if (bonus is null || !bonus.InclusDansTroncCommun)
        {
            checks.Add(new(code, libelle, Ok, bonus is null ? "Pas de module bonus." : "Module bonus optionnel, hors tronc commun."));
            return;
        }
        if (string.IsNullOrWhiteSpace(bonus.Titre) || bonus.DureeHeures is null)
        {
            checks.Add(new(code, libelle, Fail, "Module bonus inclus au tronc commun mais sans titre/durée exploitable — ne peut apparaître dans le planning."));
            return;
        }

        var bonusWords = MeaningfulWords(bonus.Titre);
        var referenced = evaluation.Any(e => bonusWords.Overlaps(MeaningfulWords(e.Nom ?? "")));

        checks.Add(referenced
            ? new(code, libelle, Ok, "Le module bonus est reflété dans l'évaluation.")
            : new(code, libelle, Fail, $"« {bonus.Titre} » est inclus au tronc commun mais n'apparaît pas dans l'évaluation."));
    }
}
