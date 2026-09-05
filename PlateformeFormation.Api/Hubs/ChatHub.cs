using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Dtos;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    public const string GroupGeneral = "general";
    private const int PreviewLength = 80;

    private readonly ApplicationDbContext _db;

    public ChatHub(ApplicationDbContext db)
    {
        _db = db;
    }

    private int CurrentUserId => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string UserGroup(int userId) => $"user-{userId}";

    private static string Preview(string contenu) =>
        contenu.Length > PreviewLength ? contenu[..PreviewLength] + "…" : contenu;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupGeneral);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(CurrentUserId));
        await base.OnConnectedAsync();
    }

    public async Task SendGroupMessage(string contenu)
    {
        if (string.IsNullOrWhiteSpace(contenu)) return;

        var senderId = CurrentUserId;
        var sender = await _db.Utilisateurs.AsNoTracking().FirstAsync(u => u.Id == senderId);

        var message = new ChatMessage
        {
            ExpediteurId = senderId,
            DestinataireId = null,
            Contenu = contenu.Trim(),
        };
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        var dto = new ChatMessageDto(
            message.Id, senderId, sender.Nom, sender.PhotoUrl,
            null, message.Contenu, message.DateEnvoi
        );

        await Clients.Group(GroupGeneral).SendAsync("ReceiveGroupMessage", dto);

        var recipientIds = await _db.Utilisateurs.AsNoTracking()
            .Where(u => u.Id != senderId && u.Actif && u.StatutCompte == StatutCompte.VALIDE)
            .Select(u => u.Id)
            .ToListAsync();

        await NotifyRecipientsAsync(
            recipientIds, "CHAT_MESSAGE_GROUP",
            $"{sender.Nom} (Salon général): {Preview(message.Contenu)}", "/chat");
    }

    public async Task SendPrivateMessage(int destinataireId, string contenu)
    {
        if (string.IsNullOrWhiteSpace(contenu)) return;

        var senderId = CurrentUserId;
        var sender = await _db.Utilisateurs.AsNoTracking().FirstAsync(u => u.Id == senderId);

        var message = new ChatMessage
        {
            ExpediteurId = senderId,
            DestinataireId = destinataireId,
            Contenu = contenu.Trim(),
        };
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        var dto = new ChatMessageDto(
            message.Id, senderId, sender.Nom, sender.PhotoUrl,
            destinataireId, message.Contenu, message.DateEnvoi
        );

        await Clients.Groups(UserGroup(destinataireId), UserGroup(senderId))
            .SendAsync("ReceivePrivateMessage", dto);

        await NotifyRecipientsAsync(
            [destinataireId], "CHAT_MESSAGE_PRIVATE",
            $"{sender.Nom}: {Preview(message.Contenu)}", $"/chat?dest={senderId}");
    }

    private async Task NotifyRecipientsAsync(IEnumerable<int> recipientIds, string type, string contenu, string lien)
    {
        var notifications = recipientIds
            .Select(id => new Notification { UtilisateurId = id, Type = type, Contenu = contenu, Lien = lien })
            .ToList();
        if (notifications.Count == 0) return;

        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync();

        foreach (var notif in notifications)
        {
            var dto = new NotificationDto(notif.Id, notif.Type, notif.Contenu, notif.Lien, notif.Lue, notif.DateCreation);
            await Clients.Group(UserGroup(notif.UtilisateurId)).SendAsync("ReceiveNotification", dto);
        }
    }
}
