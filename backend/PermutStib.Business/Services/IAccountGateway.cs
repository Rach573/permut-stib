using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public interface IAccountGateway
{
    Task<AgentAccount> RegisterAsync(RegisterAgentCommand command, CancellationToken cancellationToken);
    Task<AuthenticatedAgent?> AuthenticateAsync(LoginCommand command, CancellationToken cancellationToken);
    Task SetStatusAsync(Guid agentId, AgentStatus status, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentAccount>> GetPendingAsync(CancellationToken cancellationToken);
}

