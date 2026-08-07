namespace PermutStib.Business.Models;

public sealed record RegisterAgentCommand(string Matricule, string PhoneNumber, string Password);

public sealed record LoginCommand(string Identifier, string Password);

public sealed record AuthenticatedAgent(Guid Id, string Matricule, AgentStatus Status, AgentRole Role);

