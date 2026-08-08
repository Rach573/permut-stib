namespace PermutStib.Data.Entities;

public sealed class SignatureAvailabilityRecord
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public string? Comment { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
