using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public interface IAdminGateway
{
    Task<AdminSummary> GetSummaryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAgent>> GetAgentsAsync(CancellationToken cancellationToken);
    Task SetAgentStatusAsync(Guid actorId, Guid agentId, AgentStatus status, string? reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminPermutation>> GetPermutationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSignature>> GetSignaturesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<HelpStatistics>> GetHelpStatisticsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAuditEntry>> GetAuditAsync(CancellationToken cancellationToken);
}
