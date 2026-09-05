using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public interface IFormationCorrectionService
{
    // Fixes what FormationQualityService flags as failing on an already-generated (possibly
    // manually edited) formation. Deterministic arithmetic is always corrected in code; an LLM call
    // is only made when content-judgment problems remain (duplicate livrable, generic objective...),
    // and only ever targets exactly those flagged problems. No source documents needed — every
    // remaining check is a rewrite of existing content, not a request for new facts.
    Task<FormationDraft> CorrectAsync(Formation formation, CancellationToken ct = default);
}
