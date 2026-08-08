using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public sealed class AdminService(IAdminGateway gateway)
{
    public Task<AdminSummary> GetSummaryAsync(CancellationToken token) => gateway.GetSummaryAsync(token);
    public Task<IReadOnlyList<AdminAgent>> GetAgentsAsync(CancellationToken token) => gateway.GetAgentsAsync(token);
    public Task<IReadOnlyList<AdminPermutation>> GetPermutationsAsync(CancellationToken token) => gateway.GetPermutationsAsync(token);
    public Task<IReadOnlyList<AdminSignature>> GetSignaturesAsync(CancellationToken token) => gateway.GetSignaturesAsync(token);
    public Task<IReadOnlyList<HelpStatistics>> GetHelpStatisticsAsync(CancellationToken token) => gateway.GetHelpStatisticsAsync(token);
    public Task<IReadOnlyList<AdminAuditEntry>> GetAuditAsync(CancellationToken token) => gateway.GetAuditAsync(token);

    public Task SetAgentStatusAsync(Guid actorId, Guid agentId, AgentStatus status, string? reason, CancellationToken token)
    {
        return gateway.SetAgentStatusAsync(actorId, agentId, status, reason, token);
    }
}
