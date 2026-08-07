namespace PermutStib.Business.Models;

public enum AgentStatus
{
    Pending,
    Active,
    Suspended,
    Rejected
}

public enum AgentRole
{
    Agent,
    Admin
}

public sealed record AgentAccount(
    Guid Id,
    string Matricule,
    string PhoneNumber,
    AgentStatus Status,
    AgentRole Role,
    DateTimeOffset CreatedAt);

