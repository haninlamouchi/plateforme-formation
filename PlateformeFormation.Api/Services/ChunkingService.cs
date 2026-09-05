using System.Text.RegularExpressions;

namespace PlateformeFormation.Api.Services;

public class ChunkingService : IChunkingService
{
    // Splits after sentence-ending punctuation followed by whitespace, so each
    // chunk boundary falls on a full sentence instead of an arbitrary word.
    private static readonly Regex SentenceBoundary = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);

    public List<string> SplitIntoChunks(string text, int chunkSizeWords = 600, int overlapWords = 100)
    {
        if (overlapWords >= chunkSizeWords)
            throw new ArgumentException("overlapWords must be smaller than chunkSizeWords.");

        if (string.IsNullOrWhiteSpace(text)) return [];

        var sentences = SentenceBoundary.Split(text.Trim())
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (sentences.Count == 0) return [];

        var chunks = new List<string>();
        var current = new List<string>();
        var currentWordCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceWordCount = CountWords(sentence);

            // An unpunctuated run of text (e.g. a list or table dump) longer than a whole
            // chunk would otherwise never close a chunk — fall back to raw word splitting for it.
            if (sentenceWordCount > chunkSizeWords)
            {
                if (current.Count > 0)
                {
                    chunks.Add(string.Join(' ', current));
                    current.Clear();
                    currentWordCount = 0;
                }
                chunks.AddRange(SplitWordsIntoChunks(sentence, chunkSizeWords, overlapWords));
                continue;
            }

            if (currentWordCount + sentenceWordCount > chunkSizeWords && current.Count > 0)
            {
                chunks.Add(string.Join(' ', current));
                current = TakeOverlapSentences(current, overlapWords);
                currentWordCount = current.Sum(CountWords);
            }

            current.Add(sentence);
            currentWordCount += sentenceWordCount;
        }

        if (current.Count > 0)
            chunks.Add(string.Join(' ', current));

        return chunks;
    }

    private static List<string> TakeOverlapSentences(List<string> sentences, int overlapWords)
    {
        var overlap = new List<string>();
        var wordCount = 0;

        for (var i = sentences.Count - 1; i >= 0 && wordCount < overlapWords; i--)
        {
            overlap.Insert(0, sentences[i]);
            wordCount += CountWords(sentences[i]);
        }

        return overlap;
    }

    private static List<string> SplitWordsIntoChunks(string text, int chunkSizeWords, int overlapWords)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var step = chunkSizeWords - overlapWords;

        for (var start = 0; start < words.Length; start += step)
        {
            var length = Math.Min(chunkSizeWords, words.Length - start);
            var slice = new ArraySegment<string>(words, start, length).ToArray();
            chunks.Add(string.Join(' ', slice));
            if (start + length >= words.Length) break;
        }

        return chunks;
    }

    private static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
