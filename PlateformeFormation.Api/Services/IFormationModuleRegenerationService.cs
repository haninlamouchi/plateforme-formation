using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public interface IFormationModuleRegenerationService
{
    // Regenerates exactly one module of an already-generated formation, grounded in the formation's
    // own source documents. Never renumbers — "numero" is pinned to the module being regenerated.
    // Preview only: the caller decides whether to keep the result, and nothing is persisted here.
    Task<ModuleCard> RegenerateAsync(
        Formation formation, int numero, List<FormationSourceDocument> sources, CancellationToken ct = default);
}
