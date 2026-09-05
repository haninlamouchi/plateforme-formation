using System.ComponentModel.DataAnnotations;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Dtos;

public record FormationDocumentDto(int DocumentId, string DocumentTitre, decimal? ScorePertinence);

public record FormationDto(
    int Id,
    string Titre,
    string? Objectifs,
    decimal? DureeEstimee,
    string? Modules,
    string? Activites,
    string? MethodesEvaluation,
    string Statut,
    int CreePar,
    string CreateurNom,
    DateTime DateCreation,
    List<FormationDocumentDto> Documents,
    int QualiteScore,
    string QualiteNiveau,
    List<PlanningJour> PlanningJours
);

public record GenerateFormationRequest(
    [Required(ErrorMessage = "L'objectif est requis.")] string Objectif,
    List<int>? DocumentIds
);

public record UpdateFormationRequest(
    [Required, MaxLength(255)] string Titre,
    string? Objectifs,
    decimal? DureeEstimee,
    string? Modules,
    string? Activites,
    string? MethodesEvaluation
);

public record UpdateFormationStatutRequest([Required] string Statut);

public record FormationContentDto(
    string? Objectifs, decimal? DureeEstimee, string? Modules, string? Activites, string? MethodesEvaluation,
    int QualiteScore, string QualiteNiveau, List<PlanningJour> PlanningJours
);

// Preview only — nothing is persisted until the user picks "Apres" and it goes through the normal
// PUT /formations/{id} update flow. A corrected formation is never applied silently.
public record FormationCorrectionPreviewDto(FormationContentDto Avant, FormationContentDto Apres);

// Preview only, same contract as FormationCorrectionPreviewDto — the regenerated module is applied
// client-side and reaches the server only through the normal PUT /formations/{id} update flow.
public record RegenerateModuleResultDto(ModuleCard Avant, ModuleCard Apres);
