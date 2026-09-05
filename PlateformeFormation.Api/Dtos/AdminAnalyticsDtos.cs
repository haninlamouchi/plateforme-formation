namespace PlateformeFormation.Api.Dtos;

public record AdminAnalyticsSummaryDto(
    int FormationsGeneratedThisMonth,
    double ValidationRatePct,
    double AvgQualityScore,
    int ChatbotQuestionsThisMonth,
    int ExportsThisMonth,
    int PdfExports,
    int PptxExports
);

// One point per day over the requested window — three series sharing the same day label, kept
// separate (not one combined stacked series) so the frontend can render three small independent
// charts, mirroring how AdminChartsDto.UploadsByMonth is a single flat series.
public record DailyStatDto(string Day, int Count);
public record ActivityTimelineDto(
    IEnumerable<DailyStatDto> Generations,
    IEnumerable<DailyStatDto> Exports,
    IEnumerable<DailyStatDto> ChatbotQuestions
);

public record DocumentUsageStatDto(int DocumentId, string Titre, int UsageCount, double AvgScorePertinence);
public record ChatbotModeStatDto(string Mode, int Count);
public record FormationNeedingAttentionDto(int Id, string Titre, int QualiteScore, string QualiteNiveau);

public record AdminAnalyticsChartsDto(
    ActivityTimelineDto ActivityTimeline,
    IEnumerable<DocumentUsageStatDto> TopDocuments,
    IEnumerable<ChatbotModeStatDto> ChatbotModeSplit,
    IEnumerable<FormationNeedingAttentionDto> FormationsNeedingAttention
);
