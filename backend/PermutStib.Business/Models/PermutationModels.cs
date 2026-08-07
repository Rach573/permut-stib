namespace PermutStib.Business.Models;

public enum PermutationStatus
{
    Open,
    ProposalReceived,
    Accepted,
    Confirmed,
    Locked,
    Cancelled,
    Rejected
}

public enum PermutationProposalStatus
{
    Pending,
    Accepted,
    Rejected,
    Withdrawn
}

public sealed record DatePeriod(DateOnly From, DateOnly To)
{
    public bool Overlaps(DatePeriod other) => From <= other.To && other.From <= To;
}

public sealed record CreatePermutationCommand(DatePeriod OwnedPeriod, DatePeriod WantedPeriod);
public sealed record ProposePermutationCommand(Guid RequestId, DatePeriod OfferedPeriod);

public sealed record PermutationProposal(
    Guid Id,
    Guid RequestId,
    Guid PartnerId,
    DatePeriod OfferedPeriod,
    PermutationProposalStatus Status,
    DateTimeOffset CreatedAt);

public sealed record PermutationDetails(
    Guid Id,
    Guid RequesterId,
    DatePeriod OwnedPeriod,
    DatePeriod WantedPeriod,
    PermutationStatus Status,
    Guid? AcceptedProposalId,
    bool RequesterConfirmed,
    bool PartnerConfirmed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LockedAt,
    IReadOnlyList<PermutationProposal> Proposals);

