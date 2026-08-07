using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Business.Services;
using PermutStib.Data.Persistence;

namespace PermutStib.Data.Repositories;

public sealed class NotificationGateway(PermutStibDbContext db) : INotificationGateway
{
    public async Task<IReadOnlyList<AgentNotification>> GetMineAsync(Guid agentId, bool unreadOnly, CancellationToken cancellationToken) =>
        await db.Notifications.Where(x => x.RecipientId == agentId && (!unreadOnly || !x.IsRead))
            .OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new AgentNotification(x.Id, x.Type, x.Message, x.EntityType, x.EntityId, x.IsRead, x.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task MarkReadAsync(Guid agentId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.RecipientId == agentId, cancellationToken)
            ?? throw new KeyNotFoundException("Notification introuvable.");
        notification.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid agentId, CancellationToken cancellationToken)
    {
        await db.Notifications.Where(x => x.RecipientId == agentId && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsRead, true), cancellationToken);
    }
}
