using PermutStib.Business.Models;
using PermutStib.Business.Abstractions;
using PermutStib.Business.Rules;

namespace PermutStib.Business.Services;

public sealed class SignatureService(ISignatureGateway gateway, IDateTimeProvider clock)
{
    public Task<SignatureDetails> CreateAsync(Guid requesterId, CreateSignatureCommand command, CancellationToken cancellationToken)
    {
        return gateway.CreateAsync(requesterId, SignatureRules.ValidateCreation(command, clock.Today), cancellationToken);
    }

    public Task<IReadOnlyList<SignatureDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.GetMineAsync(agentId, cancellationToken);

    public Task<IReadOnlyList<SignatureDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.GetAvailableAsync(agentId, cancellationToken);

    public Task<SignatureDetails> OfferAsync(Guid signerId, Guid requestId, CancellationToken cancellationToken) =>
        gateway.OfferAsync(signerId, requestId, cancellationToken);

    public Task<SignatureDetails> ConfirmSignerAsync(Guid requesterId, Guid requestId, Guid offerId, CancellationToken cancellationToken) =>
        gateway.ConfirmSignerAsync(requesterId, requestId, offerId, cancellationToken);

    public Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken) =>
        gateway.CancelAsync(requesterId, requestId, cancellationToken);

    public Task<SignatureAvailability> CreateAvailabilityAsync(Guid agentId, CreateSignatureAvailabilityCommand command, CancellationToken cancellationToken) =>
        gateway.CreateAvailabilityAsync(agentId, SignatureRules.ValidateAvailability(command, clock.Today), cancellationToken);

    public Task<IReadOnlyList<SignatureAvailability>> GetMyAvailabilitiesAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.GetMyAvailabilitiesAsync(agentId, cancellationToken);

    public Task CancelAvailabilityAsync(Guid agentId, Guid availabilityId, CancellationToken cancellationToken) =>
        gateway.CancelAvailabilityAsync(agentId, availabilityId, cancellationToken);

    public Task<IReadOnlyList<HelpStatistics>> GetHelpStatisticsAsync(CancellationToken cancellationToken) =>
        gateway.GetHelpStatisticsAsync(cancellationToken);
}
