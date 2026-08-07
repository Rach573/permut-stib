namespace PermutStib.Data.Entities;

public sealed class PermutationRecord
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public DateOnly OwnedFrom { get; set; }
    public DateOnly OwnedTo { get; set; }
    public DateOnly WantedFrom { get; set; }
    public DateOnly WantedTo { get; set; }
    public string Status { get; set; } = "Searching";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedAt { get; set; }
}

