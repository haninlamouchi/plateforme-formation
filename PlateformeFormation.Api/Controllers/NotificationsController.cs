using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NotificationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications([FromQuery] int take = 30)
    {
        take = Math.Clamp(take, 1, 100);
        var meId = GetCurrentUserId();

        var notifications = await _db.Notifications.AsNoTracking()
            .Where(n => n.UtilisateurId == meId)
            .OrderByDescending(n => n.DateCreation)
            .Take(take)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Contenu, n.Lien, n.Lue, n.DateCreation))
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var meId = GetCurrentUserId();
        var count = await _db.Notifications.CountAsync(n => n.UtilisateurId == meId && !n.Lue);
        return Ok(count);
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var meId = GetCurrentUserId();
        var notif = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UtilisateurId == meId);
        if (notif is null) return NotFound();

        notif.Lue = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Notification marked as read." });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var meId = GetCurrentUserId();
        await _db.Notifications
            .Where(n => n.UtilisateurId == meId && !n.Lue)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Lue, true));

        return Ok(new { message = "All notifications marked as read." });
    }
}
