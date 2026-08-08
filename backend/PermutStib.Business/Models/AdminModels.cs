namespace PermutStib.Business.Models;

public sealed record AdminSummary(
    int PendingAgents, int ActiveAgents, int SuspendedAgents,
    int OpenPermutations, int ConfirmedPermutations,
    int OpenSignatures, int ConfirmedSignatures, int AuditEvents);

public sealed record AdminAgent(
    Guid Id, string Matricule, string PhoneNumber, AgentStatus Status,
    AgentRole Role, DateTimeOffset CreatedAt);

public sealed record AdminPermutation(
    Guid Id, string RequesterMatricule, DateOnly OwnedFrom, DateOnly OwnedTo,
    DateOnly WantedFrom, DateOnly WantedTo, PermutationStatus Status,
    int ProposalCount, DateTimeOffset CreatedAt);

public sealed record AdminSignature(
    Guid Id, string RequesterMatricule, DateOnly ServiceDate, string? Comment,
    SignatureStatus Status, string? SignerMatricule, int OfferCount,
    DateTimeOffset CreatedAt);

public sealed record AdminAuditEntry(
    long Id, string EntityType, string EntityId, string Action,
    string? ActorMatricule, string? SubjectMatricule, string? BeforeJson,
    string? AfterJson, string? Reason, DateTimeOffset CreatedAt);

