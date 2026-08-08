using PermutStib.Business.Models;
using PermutStib.Business.Abstractions;
using PermutStib.Business.Rules;

namespace PermutStib.Business.Services;

public sealed class PermutationService(IPermutationGateway gateway, IDateTimeProvider clock)
{
    public Task<PermutationDetails> CreateAsync(Guid requesterId, CreatePermutationCommand command, CancellationToken cancellationToken)
    {
        PermutationRules.ValidateCreation(command, clock.Today);
        return gateway.CreateAsync(requesterId, command, cancellationToken);
    }

    public Task<IReadOnlyList<PermutationDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.GetMineAsync(agentId, cancellationToken);

    public Task<IReadOnlyList<PermutationDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.GetAvailableAsync(agentId, cancellationToken);

    public Task<PermutationDetails> ProposeAsync(Guid partnerId, ProposePermutationCommand command, CancellationToken cancellationToken)
    {
        PermutationRules.ValidateProposalPeriod(command.OfferedPeriod, clock.Today);
        return gateway.ProposeAsync(partnerId, command, cancellationToken);
    }

    public Task<PermutationDetails> AcceptProposalAsync(Guid requesterId, Guid requestId, Guid proposalId, CancellationToken cancellationToken) =>
        gateway.AcceptProposalAsync(requesterId, requestId, proposalId, cancellationToken);

    public Task<PermutationDetails> ConfirmAsync(Guid agentId, Guid requestId, CancellationToken cancellationToken) =>
        gateway.ConfirmAsync(agentId, requestId, cancellationToken);

    public Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken) =>
        gateway.CancelAsync(requesterId, requestId, cancellationToken);
}
