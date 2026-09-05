namespace PlateformeFormation.Api.Services;

public interface IFormationTraceabilityService
{
    // Takes the JSON array stored in Formation.Modules and returns the same array with a "sources"
    // field attached to each module (document + passage that justifies it). documentIds restricts
    // matching to the formation's own source documents.
    Task<string> AttachSourcesAsync(string modulesJson, List<int> documentIds, CancellationToken ct = default);
}
