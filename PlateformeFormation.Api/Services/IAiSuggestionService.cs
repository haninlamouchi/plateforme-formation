namespace PlateformeFormation.Api.Services;

public record CategorySuggestion(string Nom, string Description);
public record CategoryMatch(int Id, string Nom);
public record DocumentCategoryResult(int CategoryId, string CategoryNom, bool IsNew);

public interface IAiSuggestionService
{
    Task<List<CategorySuggestion>> SuggestCategoriesAsync(IEnumerable<string> existingNames);
    Task<DocumentCategoryResult?> SuggestCategoryForDocumentAsync(string extractedText, IEnumerable<CategoryMatch> categories);
}
