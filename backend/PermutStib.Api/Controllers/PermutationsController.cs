using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/permutations")]
public sealed class PermutationsController(PermutationService permutations) : AuthenticatedController
{
    [HttpPost]
    public async Task<ActionResult<PermutationDetails>> Create(CreatePermutationCommand command, CancellationToken cancellationToken) =>
        Ok(await permutations.CreateAsync(AgentId, command, cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<PermutationDetails>>> Mine(CancellationToken cancellationToken) =>
        Ok(await permutations.GetMineAsync(AgentId, cancellationToken));

    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyList<PermutationDetails>>> Available(CancellationToken cancellationToken) =>
        Ok(await permutations.GetAvailableAsync(AgentId, cancellationToken));

    [HttpPost("{requestId:guid}/proposals")]
    public async Task<ActionResult<PermutationDetails>> Propose(Guid requestId, DatePeriod offeredPeriod, CancellationToken cancellationToken) =>
        Ok(await permutations.ProposeAsync(AgentId, new(requestId, offeredPeriod), cancellationToken));

    [HttpPost("{requestId:guid}/proposals/{proposalId:guid}/accept")]
    public async Task<ActionResult<PermutationDetails>> Accept(Guid requestId, Guid proposalId, CancellationToken cancellationToken) =>
        Ok(await permutations.AcceptProposalAsync(AgentId, requestId, proposalId, cancellationToken));

    [HttpPost("{requestId:guid}/confirm")]
    public async Task<ActionResult<PermutationDetails>> Confirm(Guid requestId, CancellationToken cancellationToken) =>
        Ok(await permutations.ConfirmAsync(AgentId, requestId, cancellationToken));

    [HttpPost("{requestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid requestId, CancellationToken cancellationToken)
    {
        await permutations.CancelAsync(AgentId, requestId, cancellationToken);
        return NoContent();
    }
}
