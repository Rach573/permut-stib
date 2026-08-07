namespace PermutStib.Business.Models;

public enum NotificationType
{
    PermutationProposalReceived,
    PermutationProposalAccepted,
    PermutationConfirmed,
    PermutationLocked,
    SignatureOfferReceived,
    SignatureOfferAccepted
}

public sealed record AgentNotification(
    Guid Id,
    NotificationType Type,
    string Message,
    string EntityType,
    Guid EntityId,
    bool IsRead,
    DateTimeOffset CreatedAt);

