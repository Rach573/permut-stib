using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public sealed class PermutationService(IPermutationGateway gateway)
{
    public Task<PermutationDetails> CreateAsync(Guid requesterId, CreatePermutationCommand command, CancellationToken cancellationToken)
    {
        ValidatePeriod(command.OwnedPeriod, "possédée");
        ValidatePeriod(command.WantedPeriod, "recherchée");
        if (command.OwnedPeriod.Overlaps(command.WantedPeriod))
            throw new BusinessRuleException("Les périodes possédée et recherchée ne peuvent pas se chevaucher.");

        return gateway.CreateAsync(requesterId, command, cancellationToken);
    }

    public Task<IReadOnlyList<PermutationDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.GetMineAsync(agentId, cancellationToken);

    public Task<IReadOnlyList<PermutationDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.GetAvailableAsync(agentId, cancellationToken);

    public Task<PermutationDetails> ProposeAsync(Guid partnerId, ProposePermutationCommand command, CancellationToken cancellationToken)
    {
        ValidatePeriod(command.OfferedPeriod, "proposée");
        return gateway.ProposeAsync(partnerId, command, cancellationToken);
    }

    public Task<PermutationDetails> AcceptProposalAsync(Guid requesterId, Guid requestId, Guid proposalId, CancellationToken cancellationToken) =>
        gateway.AcceptProposalAsync(requesterId, requestId, proposalId, cancellationToken);

    public Task<PermutationDetails> ConfirmAsync(Guid agentId, Guid requestId, CancellationToken cancellationToken) =>
        gateway.ConfirmAsync(agentId, requestId, cancellationToken);

    public Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken) =>
        gateway.CancelAsync(requesterId, requestId, cancellationToken);

    private static void ValidatePeriod(DatePeriod period, string label)
    {
        if (period.To < period.From)
            throw new ArgumentException($"La période {label} est invalide.");
        if (period.From < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException($"La période {label} ne peut pas commencer dans le passé.");
    }
}

