using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminDashboardController(AdminService admin) : AuthenticatedController
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken token) => Ok(await admin.GetSummaryAsync(token));

    [HttpGet("agents")]
    public async Task<IActionResult> Agents(CancellationToken token) => Ok(await admin.GetAgentsAsync(token));

    [HttpPost("agents/{agentId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid agentId, ChangeAgentStatus command, CancellationToken token)
    {
        await admin.SetAgentStatusAsync(AgentId, agentId, command.Status, command.Reason, token);
        return NoContent();
    }

    [HttpGet("permutations")]
    public async Task<IActionResult> Permutations(CancellationToken token) => Ok(await admin.GetPermutationsAsync(token));

    [HttpGet("signatures")]
    public async Task<IActionResult> Signatures(CancellationToken token) => Ok(await admin.GetSignaturesAsync(token));

    [HttpGet("help-statistics")]
    public async Task<IActionResult> HelpStatistics(CancellationToken token) => Ok(await admin.GetHelpStatisticsAsync(token));

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(CancellationToken token) => Ok(await admin.GetAuditAsync(token));
}

public sealed record ChangeAgentStatus(AgentStatus Status, string? Reason);
