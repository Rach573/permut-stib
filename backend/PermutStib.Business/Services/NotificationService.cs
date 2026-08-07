using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public sealed class NotificationService(INotificationGateway gateway)
{
    public Task<IReadOnlyList<AgentNotification>> GetMineAsync(Guid agentId, bool unreadOnly, CancellationToken cancellationToken) =>
        gateway.GetMineAsync(agentId, unreadOnly, cancellationToken);
    public Task MarkReadAsync(Guid agentId, Guid notificationId, CancellationToken cancellationToken) =>
        gateway.MarkReadAsync(agentId, notificationId, cancellationToken);
    public Task MarkAllReadAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.MarkAllReadAsync(agentId, cancellationToken);
}

