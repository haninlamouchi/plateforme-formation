using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(int userId, string action, string? entity = null, int? entityId = null)
    {
        _db.JournalActivites.Add(new JournalActivite
        {
            UtilisateurId = userId,
            Action = action,
            EntiteConcernee = entity,
            EntiteId = entityId,
            DateAction = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
