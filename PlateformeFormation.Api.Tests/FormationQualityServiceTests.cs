using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Tests;

// One pass + one fail case per rule (R1-R13, 2026-08 v3 spec). R13 (planning never LLM-generated) has
// no corresponding quality check — it's satisfied structurally by construction (FormationPlanner is
// the only thing that ever produces a plan) and is instead covered by FormationPlannerTests.
public class FormationQualityServiceTests
{
    private static FormationQualityReport Evaluate(Formation f) => new FormationQualityService().Evaluate(f);
    private static string Status(FormationQualityReport r, string code) => r.Checks.Single(c => c.Code == code).Statut;

    // R1 — sum(modules[].dureeHeures) == dureeTotaleHeures (module_bonus included only when in the
    // core track).
    [Fact]
    public void R1_HeuresModules_Fail_WhenSumDoesNotMatchTotal()
    {
        var f = new Formation { Modules = """[{"numero":1,"dureeHeures":2},{"numero":2,"dureeHeures":3}]""", DureeEstimee = 8 };
        Assert.Equal("ECHEC", Status(Evaluate(f), "HEURES_MODULES"));
    }

    [Fact]
    public void R1_HeuresModules_Pass_WhenSumMatchesTotal()
    {
        var f = new Formation { Modules = """[{"numero":1,"dureeHeures":2},{"numero":2,"dureeHeures":3}]""", DureeEstimee = 5 };
        Assert.Equal("OK", Status(Evaluate(f), "HEURES_MODULES"));
    }

    // R2 — objectif starts with "Être capable de"/"Savoir" exactly once (never duplicated) and no
    // banned non-observable verb.
    [Fact]
    public void R2_ObjectifsMesurables_Fail_WhenPrefixDuplicated()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"dureeHeures":2,"objectif":"Être capable de être capable de construire un modèle"}]""",
            DureeEstimee = 2,
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "OBJECTIFS_MESURABLES"));
    }

    [Fact]
    public void R2_ObjectifsMesurables_Pass_WhenPrefixPresentOnce()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"dureeHeures":2,"objectif":"Être capable de construire un modèle prédictif"}]""",
            DureeEstimee = 2,
        };
        Assert.Equal("OK", Status(Evaluate(f), "OBJECTIFS_MESURABLES"));
    }

    // Regression: the check must recognize the elided "Être capable d'" form (produced by
    // FormationHarmonizer.ApplyElisionToObjectifs before a verb starting with a vowel/h) as a valid
    // prefix, not flag it as missing — a real bug where the elision fix and this check disagreed on
    // what counts as "prefixed", causing every generation with a vowel-start verb to fail validation
    // 3 times in a row.
    [Fact]
    public void R2_ObjectifsMesurables_Pass_WhenPrefixIsElided()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"dureeHeures":2,"objectif":"Être capable d'analyser un jeu de données métier"}]""",
            DureeEstimee = 2,
        };
        Assert.Equal("OK", Status(Evaluate(f), "OBJECTIFS_MESURABLES"));
    }

    // Regression: duplicate detection must not misalign when the two repeated prefixes use different
    // forms (elided vs not) — a naive fixed-length slice breaks here since "d'" and "de" normalize to
    // different lengths.
    [Fact]
    public void R2_ObjectifsMesurables_Fail_WhenElidedPrefixDuplicated()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"dureeHeures":2,"objectif":"Être capable d'être capable d'analyser un jeu de données"}]""",
            DureeEstimee = 2,
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "OBJECTIFS_MESURABLES"));
    }

    // R3 — competencesPrerequises of module i only reference modules numbered < i (no competence
    // tested before it's taught). This replaces the old lexical-heuristic "exercice de POO avant son
    // module" detection with a structural, provably-correct check on the numbered dependency graph.
    [Fact]
    public void R3_ProgressionCompetences_Fail_WhenModuleReferencesLaterModule()
    {
        // Exact reported bug: an exercise in module 1 ("Introduction à l'IA") mobilizing a competence
        // (OOP) only taught in module 2 — expressed here as module 1 forward-referencing module 2.
        var f = new Formation
        {
            Modules = """
                [
                  {"numero":1,"titre":"Introduction à l'IA","dureeHeures":2,"competencesPrerequises":[2]},
                  {"numero":2,"titre":"Programmation orientée objet","dureeHeures":3,"competencesPrerequises":[]}
                ]
                """,
            DureeEstimee = 5,
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "PROGRESSION_COMPETENCES"));
    }

    [Fact]
    public void R3_ProgressionCompetences_Pass_WhenReferencesAreEarlierOnly()
    {
        var f = new Formation
        {
            Modules = """
                [
                  {"numero":1,"titre":"Introduction","dureeHeures":2,"competencesPrerequises":[]},
                  {"numero":2,"titre":"Avancé","dureeHeures":3,"competencesPrerequises":[1]}
                ]
                """,
            DureeEstimee = 5,
        };
        Assert.Equal("OK", Status(Evaluate(f), "PROGRESSION_COMPETENCES"));
    }

    // R4 — no livrable text identical or near-identical (> 85% word overlap) to another, even across
    // a reutiliseLivrableModule chain (must describe an evolution, not a repeat).
    [Fact]
    public void R4_LivrableUnique_Fail_WhenTwoLivrablesAreWordForWordIdentical()
    {
        var f = new Formation
        {
            Modules = """
                [
                  {"numero":1,"titre":"Module 1","dureeHeures":2,"livrable":"Rapport d'analyse du dataset clients pour le projet CRM"},
                  {"numero":2,"titre":"Module 2","dureeHeures":2,"livrable":"Rapport d'analyse du dataset clients pour le projet CRM"}
                ]
                """,
            DureeEstimee = 4,
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "LIVRABLE_UNIQUE"));
    }

    [Fact]
    public void R4_LivrableUnique_Pass_WhenLivrablesAreDistinct()
    {
        var f = new Formation
        {
            Modules = """
                [
                  {"numero":1,"titre":"Module 1","dureeHeures":2,"livrable":"Rapport d'analyse exploratoire du dataset clients"},
                  {"numero":2,"titre":"Module 2","dureeHeures":2,"livrable":"Modèle de scoring déployé sur l'environnement de test"}
                ]
                """,
            DureeEstimee = 4,
        };
        Assert.Equal("OK", Status(Evaluate(f), "LIVRABLE_UNIQUE"));
    }

    // R4 boundary — the check must pass at 85% or below and fail strictly above it (word-overlap
    // ratio = shared / max(count1, count2)). Word lists are constructed with exact counts so the
    // ratio is provable rather than approximated from natural sentences.
    [Fact]
    public void R4_LivrableUnique_Pass_AtSeuil84Percent()
    {
        // 19 meaningful words each, 16 shared -> 16/19 ≈ 84.2%, must stay under the > 85% trigger.
        var baseWords = Enumerable.Range(1, 19).Select(i => $"terme{i:00}").ToArray();
        var livrableA = string.Join(" ", baseWords);
        var livrableB = string.Join(" ", baseWords.Take(16).Concat(["autre01", "autre02", "autre03"]));

        var f = new Formation
        {
            Modules = $$"""
                [
                  {"numero":1,"titre":"Module 1","dureeHeures":2,"livrable":"{{livrableA}}"},
                  {"numero":2,"titre":"Module 2","dureeHeures":2,"livrable":"{{livrableB}}"}
                ]
                """,
            DureeEstimee = 4,
        };
        Assert.Equal("OK", Status(Evaluate(f), "LIVRABLE_UNIQUE"));
    }

    [Fact]
    public void R4_LivrableUnique_Fail_AtSeuil86Percent()
    {
        // 21 meaningful words each, 18 shared -> 18/21 ≈ 85.7%, just over the > 85% trigger.
        var baseWords = Enumerable.Range(1, 21).Select(i => $"terme{i:00}").ToArray();
        var livrableC = string.Join(" ", baseWords);
        var livrableD = string.Join(" ", baseWords.Take(18).Concat(["autre01", "autre02", "autre03"]));

        var f = new Formation
        {
            Modules = $$"""
                [
                  {"numero":1,"titre":"Module 1","dureeHeures":2,"livrable":"{{livrableC}}"},
                  {"numero":2,"titre":"Module 2","dureeHeures":2,"livrable":"{{livrableD}}"}
                ]
                """,
            DureeEstimee = 4,
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "LIVRABLE_UNIQUE"));
    }

    // R5 — sum(evaluation[].pct) == 100 exactly.
    [Fact]
    public void R5_Ponderation100_Fail_WhenSumIsNot100()
    {
        var f = new Formation { MethodesEvaluation = """[{"nom":"A","pct":40},{"nom":"B","pct":40}]""" };
        Assert.Equal("ECHEC", Status(Evaluate(f), "PONDERATION_100"));
    }

    [Fact]
    public void R5_Ponderation100_Pass_WhenSumIs100()
    {
        var f = new Formation { MethodesEvaluation = """[{"nom":"A","pct":60},{"nom":"B","pct":40}]""" };
        Assert.Equal("OK", Status(Evaluate(f), "PONDERATION_100"));
    }

    // R6 — at least one evaluation entry has estEvaluationContinue == true with pct in [20, 30].
    [Fact]
    public void R6_EvaluationContinue_Fail_WhenNoneMarkedContinuous()
    {
        var f = new Formation { MethodesEvaluation = """[{"nom":"A","pct":60,"estEvaluationContinue":false},{"nom":"B","pct":40,"estEvaluationContinue":false}]""" };
        Assert.Equal("ECHEC", Status(Evaluate(f), "EVALUATION_CONTINUE"));
    }

    [Fact]
    public void R6_EvaluationContinue_Pass_WhenPresentInRange()
    {
        var f = new Formation { MethodesEvaluation = """[{"nom":"Continue","pct":25,"estEvaluationContinue":true},{"nom":"B","pct":75,"estEvaluationContinue":false}]""" };
        Assert.Equal("OK", Status(Evaluate(f), "EVALUATION_CONTINUE"));
    }

    // R7 — no evaluation entry named "attestation"/"certification" (a consequence of success, never
    // a graded item). Exact reported bug: "Attestation" counted at 20% of the evaluation.
    [Fact]
    public void R7_AttestationNonPonderee_Fail_WhenAttestationIsWeighted()
    {
        var f = new Formation { MethodesEvaluation = """[{"nom":"Étude de cas","pct":80,"estEvaluationContinue":false},{"nom":"Attestation de compétences","pct":20,"estEvaluationContinue":false}]""" };
        Assert.Equal("ECHEC", Status(Evaluate(f), "ATTESTATION_NON_PONDEREE"));
    }

    [Fact]
    public void R7_AttestationNonPonderee_Pass_WhenNoneFound()
    {
        var f = new Formation { MethodesEvaluation = """[{"nom":"Étude de cas","pct":100,"estEvaluationContinue":false}]""" };
        Assert.Equal("OK", Status(Evaluate(f), "ATTESTATION_NON_PONDEREE"));
    }

    // R8 — module_remediation must reference an existing module (hard structural failure otherwise).
    [Fact]
    public void R8_RemediationCiblee_Fail_WhenModuleDoesNotExist()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"titre":"Module 1","dureeHeures":2}]""",
            Objectifs = """{"testPositionnement":{"objectif":"Teste X","exercice":"Y","moduleRemediation":9}}""",
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "REMEDIATION_CIBLEE"));
    }

    [Fact]
    public void R8_RemediationCiblee_Pass_WhenModuleExists()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"titre":"Module 1","dureeHeures":2}]""",
            Objectifs = """{"testPositionnement":{"moduleRemediation":1}}""",
        };
        Assert.Equal("OK", Status(Evaluate(f), "REMEDIATION_CIBLEE"));
    }

    // R9 — a module bonus included in the core track must have a real duration (otherwise it could
    // never appear in the computed planning) and must show up in the evaluation. Exact reported bug:
    // a bonus module counted in the total hours but absent from the plan.
    [Fact]
    public void R9_ModuleBonusCoherent_Fail_WhenIncludedButAbsentFromEvaluation()
    {
        var f = new Formation
        {
            Objectifs = """{"moduleBonus":{"inclusDansTroncCommun":true,"titre":"Approfondissement Kubernetes","dureeHeures":2}}""",
            MethodesEvaluation = """[{"nom":"Étude de cas","pct":100,"estEvaluationContinue":false}]""",
            Modules = "[]",
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "MODULE_BONUS_COHERENT"));
    }

    [Fact]
    public void R9_ModuleBonusCoherent_Pass_WhenReferencedInEvaluation()
    {
        var f = new Formation
        {
            Objectifs = """{"moduleBonus":{"inclusDansTroncCommun":true,"titre":"Approfondissement Kubernetes","dureeHeures":2}}""",
            MethodesEvaluation = """[{"nom":"Évaluation Kubernetes avancé","pct":100,"estEvaluationContinue":false}]""",
            Modules = "[]",
        };
        Assert.Equal("OK", Status(Evaluate(f), "MODULE_BONUS_COHERENT"));
    }

    // R10 — at least 3 ressourcesPedagogiques, none identical to a source document. Exact reported
    // bug: the resource list is just a copy of the source documents.
    [Fact]
    public void R10_Ressources_Fail_WhenIdenticalToSource()
    {
        var f = new Formation
        {
            Objectifs = """{"sourcesUtilisees":["Guide Python avancé"],"ressourcesPedagogiques":["Guide Python avancé","Jeu de données CSV","Notebook Jupyter"]}""",
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "RESSOURCES"));
    }

    [Fact]
    public void R10_Ressources_Pass_WhenDistinctAndEnough()
    {
        var f = new Formation
        {
            Objectifs = """{"sourcesUtilisees":["Guide Python avancé"],"ressourcesPedagogiques":["Cheat-sheet Pandas","Jeu de données ventes_trimestrielles.csv","Notebook Jupyter pré-configuré"]}""",
        };
        Assert.Equal("OK", Status(Evaluate(f), "RESSOURCES"));
    }

    // R11 — each module.contenu has at least 4 items, each a full sentence (> 8 words) — rejects
    // keyword-list content. Exact reported bug: contenu with only 1-2 word keywords.
    [Fact]
    public void R11_ContenuDetaille_Fail_WhenItemsAreBareKeywords()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"dureeHeures":2,"contenu":["Modélisation ML","Régression","Classification","Clustering"]}]""",
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "CONTENU_DETAILLE"));
    }

    [Fact]
    public void R11_ContenuDetaille_Pass_WhenItemsAreFullSentences()
    {
        var f = new Formation
        {
            Modules = """
                [{
                  "numero":1,"dureeHeures":2,
                  "contenu":[
                    "Construction d'un modèle de classification supervisée à partir d'un jeu de données réel fourni.",
                    "Interprétation des métriques de performance telles que la précision et le rappel du modèle.",
                    "Comparaison de plusieurs algorithmes de classification sur le même jeu de données d'entraînement.",
                    "Identification des cas de surapprentissage à partir de la courbe d'apprentissage du modèle."
                  ]
                }]
                """,
        };
        Assert.Equal("OK", Status(Evaluate(f), "CONTENU_DETAILLE"));
    }

    // R12 — exercice_formatif.consigne > 15 words AND criteresReussite has >= 2 items — rejects a
    // one-line "exercice guidé" placeholder.
    [Fact]
    public void R12_ExerciceDetaille_Fail_WhenConsigneIsAOneLinePlaceholder()
    {
        var f = new Formation
        {
            Modules = """[{"numero":1,"dureeHeures":2,"exerciceFormatif":{"type":"exercice_guide","consigne":"Exercice guidé sur la régression","criteresReussite":["Modèle fonctionnel"]}}]""",
        };
        Assert.Equal("ECHEC", Status(Evaluate(f), "EXERCICE_DETAILLE"));
    }

    [Fact]
    public void R12_ExerciceDetaille_Pass_WhenConsigneIsActionableWithCriteria()
    {
        var f = new Formation
        {
            Modules = """
                [{
                  "numero":1,"dureeHeures":2,
                  "exerciceFormatif":{
                    "type":"exercice_guide",
                    "consigne":"À partir du jeu de données fourni (ventes_trimestrielles.csv), construire un modèle de régression linéaire prédisant le chiffre d'affaires du trimestre suivant.",
                    "criteresReussite":["Le modèle est entraîné sans erreur sur le jeu de données fourni","Le participant explique le choix des variables retenues"],
                    "materiel":"Notebook Jupyter pré-configuré, dataset fourni",
                    "dureeMin":30
                  }
                }]
                """,
        };
        Assert.Equal("OK", Status(Evaluate(f), "EXERCICE_DETAILLE"));
    }
}
