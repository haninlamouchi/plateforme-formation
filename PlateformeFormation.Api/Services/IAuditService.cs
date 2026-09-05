namespace PlateformeFormation.Api.Services;

public interface IAuditService
{
    Task LogAsync(int userId, string action, string? entity = null, int? entityId = null);
}
