namespace PermutStib.Business.Models;

public enum SignatureStatus
{
    Open,
    ProposalReceived,
    Confirmed,
    Locked,
    Cancelled
}

public enum SignatureOfferStatus
{
    Pending,
    Selected,
    Withdrawn,
    Rejected
}

public sealed record CreateSignatureCommand(DateOnly ServiceDate, string? Comment);
public sealed record CreateSignatureAvailabilityCommand(DateOnly ServiceDate, string? Comment);

public sealed record SignatureAvailability(
    Guid Id,
    Guid AgentId,
    DateOnly ServiceDate,
    string? Comment,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record SignatureOffer(
    Guid Id,
    Guid RequestId,
    Guid SignerId,
    Guid? AvailabilityId,
    SignatureOfferStatus Status,
    DateTimeOffset CreatedAt);

public sealed record SignatureDetails(
    Guid Id,
    Guid RequesterId,
    DateOnly ServiceDate,
    string? Comment,
    SignatureStatus Status,
    Guid? SignerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LockedAt,
    IReadOnlyList<SignatureOffer> Offers);

public sealed record HelpStatistics(
    Guid AgentId,
    string Matricule,
    int SignaturesReceived,
    int SignaturesGiven,
    int SignatureOffers,
    decimal? HelpRatio);
