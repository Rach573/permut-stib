using PermutStib.Business.Models;

namespace PermutStib.Data.Entities;

public sealed class NotificationRecord
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public NotificationType Type { get; set; }
    public required string Message { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

