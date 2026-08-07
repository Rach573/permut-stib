using PermutStib.Business.Models;

namespace PermutStib.Data.Entities;

public sealed class PermutationRecord
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public DateOnly OwnedFrom { get; set; }
    public DateOnly OwnedTo { get; set; }
    public DateOnly WantedFrom { get; set; }
    public DateOnly WantedTo { get; set; }
    public PermutationStatus Status { get; set; } = PermutationStatus.Open;
    public Guid? AcceptedProposalId { get; set; }
    public bool RequesterConfirmed { get; set; }
    public bool PartnerConfirmed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedAt { get; set; }
    public uint Version { get; set; }
    public List<PermutationProposalRecord> Proposals { get; set; } = [];
}
