using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public interface IPermutationGateway
{
    Task<PermutationDetails> CreateAsync(Guid requesterId, CreatePermutationCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<PermutationDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PermutationDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken);
    Task<PermutationDetails> ProposeAsync(Guid partnerId, ProposePermutationCommand command, CancellationToken cancellationToken);
    Task<PermutationDetails> AcceptProposalAsync(Guid requesterId, Guid requestId, Guid proposalId, CancellationToken cancellationToken);
    Task<PermutationDetails> ConfirmAsync(Guid agentId, Guid requestId, CancellationToken cancellationToken);
    Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken);
}

