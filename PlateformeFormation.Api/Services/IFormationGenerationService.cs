namespace PlateformeFormation.Api.Services;

public record FormationSourceDocument(int DocumentId, string Titre, string Content);

public record FormationDraft(
    string Titre,
    string Objectifs,
    decimal? DureeEstimeeHeures,
    string Modules,
    string Activites,
    string MethodesEvaluation
);

// Diagnostic snapshot of one generate/correct attempt — exposed so callers (production logging, the
// standalone reliability benchmark) can see exactly what failed at each step, not just the final
// outcome. ErreursEchec is empty when EstValide is true.
public record GenerationAttemptLog(int Attempt, bool EstValide, List<string> ErreursEchec);

public interface IFormationGenerationService
{
    Task<FormationDraft> GenerateAsync(
        string objectif, List<FormationSourceDocument> sources,
        Action<GenerationAttemptLog>? onAttempt = null, CancellationToken ct = default);
}
