namespace PlateformeFormation.Api.Dtos;

public record PaginatedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize);
