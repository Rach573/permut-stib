using PermutStib.Business.Models;

namespace PermutStib.Data.Entities;

public sealed class SignatureRecord
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public string? Comment { get; set; }
    public Guid? SignerId { get; set; }
    public SignatureStatus Status { get; set; } = SignatureStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedAt { get; set; }
    public uint Version { get; set; }
    public List<SignatureOfferRecord> Offers { get; set; } = [];
}
