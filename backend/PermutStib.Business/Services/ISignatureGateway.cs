using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public interface ISignatureGateway
{
    Task<SignatureDetails> CreateAsync(Guid requesterId, CreateSignatureCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<SignatureDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SignatureDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken);
    Task<SignatureDetails> OfferAsync(Guid signerId, Guid requestId, CancellationToken cancellationToken);
    Task<SignatureDetails> ConfirmSignerAsync(Guid requesterId, Guid requestId, Guid offerId, CancellationToken cancellationToken);
    Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken);
    Task<SignatureAvailability> CreateAvailabilityAsync(Guid agentId, CreateSignatureAvailabilityCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<SignatureAvailability>> GetMyAvailabilitiesAsync(Guid agentId, CancellationToken cancellationToken);
    Task CancelAvailabilityAsync(Guid agentId, Guid availabilityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HelpStatistics>> GetHelpStatisticsAsync(CancellationToken cancellationToken);
}
