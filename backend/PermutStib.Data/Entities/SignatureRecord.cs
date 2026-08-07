namespace PermutStib.Data.Entities;

public sealed class SignatureRecord
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public Guid? SignerId { get; set; }
    public string Status { get; set; } = "Searching";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedAt { get; set; }
}

