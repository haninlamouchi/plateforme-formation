namespace PlateformeFormation.Api.Services;

public enum ChatIntent
{
    Question,
    Resume,
    Competences
}

public static class ChatIntentDetector
{
    private static readonly string[] CompetenceKeywords =
        ["compétence", "competence", "savoir-faire", "savoir faire", "skill", "acquis pédagogique", "acquis pedagogique"];

    private static readonly string[] ResumeKeywords =
        ["résum", "resum", "synthèse", "synthese", "summar", "de quoi parle", "en bref"];

    public static ChatIntent Detect(string question)
    {
        var q = question.ToLowerInvariant();

        if (CompetenceKeywords.Any(q.Contains))
            return ChatIntent.Competences;

        if (ResumeKeywords.Any(q.Contains))
            return ChatIntent.Resume;

        return ChatIntent.Question;
    }
}
