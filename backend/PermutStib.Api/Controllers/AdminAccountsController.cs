using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermutStib.Business.Services;

namespace PermutStib.Api.Controllers;

[ApiController]
[Route("api/admin/accounts")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminAccountsController(AccountService accounts) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken) =>
        Ok(await accounts.GetPendingAsync(cancellationToken));

    [HttpPost("{agentId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid agentId, CancellationToken cancellationToken)
    {
        await accounts.ApproveAsync(agentId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{agentId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid agentId, CancellationToken cancellationToken)
    {
        await accounts.RejectAsync(agentId, cancellationToken);
        return NoContent();
    }
}

