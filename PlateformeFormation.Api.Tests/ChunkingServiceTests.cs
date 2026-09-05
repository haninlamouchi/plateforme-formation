using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Tests;

public class ChunkingServiceTests
{
    private readonly ChunkingService _sut = new();

    [Fact]
    public void SplitIntoChunks_ReturnsEmpty_WhenTextIsEmptyOrWhitespace()
    {
        Assert.Empty(_sut.SplitIntoChunks(""));
        Assert.Empty(_sut.SplitIntoChunks("   \n\t  "));
    }

    [Fact]
    public void SplitIntoChunks_ReturnsSingleChunk_WhenTextFitsInOneChunk()
    {
        var text = "Ceci est une phrase. Voici une deuxième phrase.";

        var chunks = _sut.SplitIntoChunks(text, chunkSizeWords: 600, overlapWords: 100);

        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
    }

    [Fact]
    public void SplitIntoChunks_SplitsOnSentenceBoundaries_NotMidSentence()
    {
        var text = string.Concat(Enumerable.Range(1, 20).Select(i => $"Phrase numero {i} avec plusieurs mots dedans. "));

        var chunks = _sut.SplitIntoChunks(text, chunkSizeWords: 20, overlapWords: 5);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.EndsWith(".", c.TrimEnd()));
    }

    [Fact]
    public void SplitIntoChunks_CarriesOverlapWordsIntoNextChunk()
    {
        var sentences = Enumerable.Range(1, 10).Select(i => $"Phrase numero {i} ici.").ToArray();
        var text = string.Join(' ', sentences);

        var chunks = _sut.SplitIntoChunks(text, chunkSizeWords: 12, overlapWords: 4);

        Assert.True(chunks.Count > 1);
        // The tail of the first chunk should reappear at the head of the second chunk.
        var firstChunkWords = chunks[0].Split(' ');
        var overlapTail = string.Join(' ', firstChunkWords[^4..]);
        Assert.StartsWith(overlapTail, chunks[1]);
    }

    [Fact]
    public void SplitIntoChunks_FallsBackToWordSplitting_ForUnpunctuatedRunLongerThanChunk()
    {
        // A single "sentence" (no terminal punctuation) longer than chunkSizeWords, e.g. a table dump.
        var longRun = string.Join(' ', Enumerable.Range(1, 50).Select(i => $"mot{i}"));

        var chunks = _sut.SplitIntoChunks(longRun, chunkSizeWords: 10, overlapWords: 2);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Split(' ').Length <= 10));
    }

    [Fact]
    public void SplitIntoChunks_Throws_WhenOverlapNotSmallerThanChunkSize()
    {
        Assert.Throws<ArgumentException>(() => _sut.SplitIntoChunks("un texte quelconque.", chunkSizeWords: 10, overlapWords: 10));
    }
}
