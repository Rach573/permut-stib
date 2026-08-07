using PermutStib.Business.Models;

namespace PermutStib.Data.Entities;

public sealed class PermutationProposalRecord
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public required PermutationRecord Request { get; set; }
    public Guid PartnerId { get; set; }
    public DateOnly OfferedFrom { get; set; }
    public DateOnly OfferedTo { get; set; }
    public PermutationProposalStatus Status { get; set; } = PermutationProposalStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
