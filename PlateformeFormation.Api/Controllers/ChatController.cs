using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ChatController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<ChatUserDto>>> GetChatUsers()
    {
        var meId = GetCurrentUserId();
        var users = await _db.Utilisateurs.AsNoTracking()
            .Where(u => u.Id != meId && u.Actif && u.StatutCompte == StatutCompte.VALIDE)
            .OrderBy(u => u.Nom)
            .Select(u => new ChatUserDto(u.Id, u.Nom, u.Email, u.Role.ToString(), u.PhotoUrl))
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("group")]
    public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetGroupHistory(
        [FromQuery] DateTime? before, [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);

        var query = _db.ChatMessages.AsNoTracking()
            .Include(m => m.Expediteur)
            .Where(m => m.DestinataireId == null);

        if (before.HasValue) query = query.Where(m => m.DateEnvoi < before.Value);

        var messages = await query
            .OrderByDescending(m => m.DateEnvoi)
            .Take(take)
            .OrderBy(m => m.DateEnvoi)
            .Select(m => new ChatMessageDto(
                m.Id, m.ExpediteurId, m.Expediteur!.Nom, m.Expediteur.PhotoUrl,
                null, m.Contenu, m.DateEnvoi))
            .ToListAsync();

        return Ok(messages);
    }

    [HttpGet("private/{userId:int}")]
    public async Task<ActionResult<IEnumerable<ChatMessageDto>>> GetPrivateHistory(
        int userId, [FromQuery] DateTime? before, [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var meId = GetCurrentUserId();

        var query = _db.ChatMessages
            .Include(m => m.Expediteur)
            .Where(m =>
                (m.ExpediteurId == meId && m.DestinataireId == userId) ||
                (m.ExpediteurId == userId && m.DestinataireId == meId));

        if (before.HasValue) query = query.Where(m => m.DateEnvoi < before.Value);

        var messages = await query
            .OrderByDescending(m => m.DateEnvoi)
            .Take(take)
            .OrderBy(m => m.DateEnvoi)
            .ToListAsync();

        var unread = messages.Where(m => m.DestinataireId == meId && !m.Lu).ToList();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.Lu = true;
            await _db.SaveChangesAsync();
        }

        var dtos = messages.Select(m => new ChatMessageDto(
            m.Id, m.ExpediteurId, m.Expediteur!.Nom, m.Expediteur.PhotoUrl,
            m.DestinataireId, m.Contenu, m.DateEnvoi));

        return Ok(dtos);
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IEnumerable<ChatConversationDto>>> GetConversations()
    {
        var meId = GetCurrentUserId();

        var privateMessages = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.DestinataireId != null && (m.ExpediteurId == meId || m.DestinataireId == meId))
            .OrderByDescending(m => m.DateEnvoi)
            .Select(m => new
            {
                PartnerId = m.ExpediteurId == meId ? m.DestinataireId!.Value : m.ExpediteurId,
                m.Contenu,
                m.DateEnvoi,
                IsUnreadForMe = m.DestinataireId == meId && !m.Lu,
            })
            .ToListAsync();

        if (privateMessages.Count == 0) return Ok(Array.Empty<ChatConversationDto>());

        var partnerIds = privateMessages.Select(m => m.PartnerId).Distinct().ToList();
        var partners = await _db.Utilisateurs.AsNoTracking()
            .Where(u => partnerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var conversations = privateMessages
            .GroupBy(m => m.PartnerId)
            .Where(g => partners.ContainsKey(g.Key))
            .Select(g =>
            {
                var last = g.First(); // already ordered by DateEnvoi desc
                var partner = partners[g.Key];
                return new ChatConversationDto(
                    partner.Id, partner.Nom, partner.PhotoUrl,
                    last.Contenu, last.DateEnvoi,
                    g.Count(m => m.IsUnreadForMe)
                );
            })
            .OrderByDescending(c => c.DateDernierMessage)
            .ToList();

        return Ok(conversations);
    }
}
