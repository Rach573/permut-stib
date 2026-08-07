namespace PermutStib.Data.Entities;

public sealed class AuditRecord
{
    public long Id { get; set; }
    public Guid? ActorId { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public required string Action { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

