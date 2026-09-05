namespace PlateformeFormation.Api.Services;

public interface IChunkingService
{
    List<string> SplitIntoChunks(string text, int chunkSizeWords = 600, int overlapWords = 100);
}
