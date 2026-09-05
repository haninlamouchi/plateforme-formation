namespace PlateformeFormation.Api.Models;

// Typed shapes of the JSON blobs stored in Formation.Objectifs/Modules/MethodesEvaluation. This is
// the "v3" schema (2026-08 spec): richer per-module content-depth fields (grille d'évaluation, notes
// formateur, exercice_formatif with an actionable consigne + success criteria) plus module numbering
// with explicit integer dependency references so progression/chaining can be verified structurally
// rather than by lexical-overlap guessing. All properties are nullable/optional so older or
// partially-edited data degrades gracefully instead of throwing.

public record ModuleMethode(string? Type, double? PctTheorie, double? PctPratique);

public record ModuleExerciceFormatif(
    string? Type, string? Consigne, List<string>? CriteresReussite, string? Materiel, int? DureeMin);

public record GrilleEvaluationItem(string? Critere, double? Pct);

// A source passage backing a module — attached by FormationTraceabilityService, unrelated to the
// generation schema itself.
public record ModuleSource(int DocumentId, string DocumentTitre, int Ordre, string Extrait, double Score);

public record ModuleCard(
    int Numero, string? Titre, double? DureeHeures, string? Objectif,
    ModuleMethode? Methode, List<string>? Contenu, ModuleExerciceFormatif? ExerciceFormatif,
    string? Livrable, int? ReutiliseLivrableModule, List<int>? CompetencesPrerequises,
    List<GrilleEvaluationItem>? GrilleEvaluation, List<string>? NotesFormateur,
    List<ModuleSource>? Sources);

public record ModuleBonus(bool InclusDansTroncCommun, string? Titre, double? DureeHeures, List<string>? Contenu);

public record TestPositionnementData(
    string? Objectif, int? QcmQuestions, string? Exercice, double? SeuilParcoursStandardPct, int? ModuleRemediation);

public record ModalitesPratiques(string? Format, string? Intervenant, List<string>? MaterielRequis);

public record BeneficeEntreprise(string? Titre, string? Justification);

public record ActivitePedagogique(string? Nom, string? Format, string? Description);

// ActivitesPedagogiques deliberately lives outside this bundle — it's stored in the dedicated
// Formation.Activites column (revived for this schema; the previous v2 schema had dropped the
// notion of a separate "activités" section entirely, this spec brings it back).
public record ObjectifsData(
    string? SousTitre, string? Contexte, string? PublicCible, string? PrerequisDetailles,
    ModalitesPratiques? ModalitesPratiques, List<BeneficeEntreprise>? BeneficesEntreprise,
    List<string>? SourcesUtilisees, List<string>? LacunesContexte,
    TestPositionnementData? TestPositionnement, ModuleBonus? ModuleBonus,
    List<string>? RessourcesPedagogiques, List<string>? DocumentsSources);

public record EvaluationItem(string? Nom, double? Pct, bool EstEvaluationContinue);
