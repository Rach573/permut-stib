using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public interface INotificationGateway
{
    Task<IReadOnlyList<AgentNotification>> GetMineAsync(Guid agentId, bool unreadOnly, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid agentId, Guid notificationId, CancellationToken cancellationToken);
    Task MarkAllReadAsync(Guid agentId, CancellationToken cancellationToken);
}

