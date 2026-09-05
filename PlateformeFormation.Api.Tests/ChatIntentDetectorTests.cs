using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Tests;

public class ChatIntentDetectorTests
{
    [Theory]
    [InlineData("Quelles sont les compétences abordées dans ce document ?")]
    [InlineData("Quel savoir-faire est développé ici ?")]
    [InlineData("What skill does this cover?")]
    [InlineData("Liste les acquis pédagogiques.")]
    public void Detect_ReturnsCompetences_ForCompetenceKeywords(string question)
    {
        Assert.Equal(ChatIntent.Competences, ChatIntentDetector.Detect(question));
    }

    [Theory]
    [InlineData("Peux-tu résumer ce document ?")]
    [InlineData("Fais-moi une synthèse.")]
    [InlineData("Can you summarize this?")]
    [InlineData("De quoi parle ce document ?")]
    [InlineData("En bref, que dit ce guide ?")]
    public void Detect_ReturnsResume_ForSummaryKeywords(string question)
    {
        Assert.Equal(ChatIntent.Resume, ChatIntentDetector.Detect(question));
    }

    [Theory]
    [InlineData("Comment évaluer les apprentissages des étudiants ?")]
    [InlineData("Quelle est la durée recommandée pour ce module ?")]
    public void Detect_ReturnsQuestion_WhenNoKeywordMatches(string question)
    {
        Assert.Equal(ChatIntent.Question, ChatIntentDetector.Detect(question));
    }

    [Fact]
    public void Detect_CompetenceKeyword_TakesPrecedenceOverResumeKeyword()
    {
        // Both keyword sets present in the same question: competence check runs first.
        var question = "Résume les compétences abordées dans ce document.";

        Assert.Equal(ChatIntent.Competences, ChatIntentDetector.Detect(question));
    }

    [Fact]
    public void Detect_IsCaseInsensitive()
    {
        Assert.Equal(ChatIntent.Resume, ChatIntentDetector.Detect("RÉSUME CE DOCUMENT"));
    }
}
