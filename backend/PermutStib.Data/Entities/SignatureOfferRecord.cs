using PermutStib.Business.Models;

namespace PermutStib.Data.Entities;

public sealed class SignatureOfferRecord
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public required SignatureRecord Request { get; set; }
    public Guid SignerId { get; set; }
    public SignatureOfferStatus Status { get; set; } = SignatureOfferStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
