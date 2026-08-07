using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/signatures")]
public sealed class SignaturesController(SignatureService signatures) : AuthenticatedController
{
    [HttpPost]
    public async Task<ActionResult<SignatureDetails>> Create(CreateSignatureCommand command, CancellationToken cancellationToken) =>
        Ok(await signatures.CreateAsync(AgentId, command, cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<SignatureDetails>>> Mine(CancellationToken cancellationToken) =>
        Ok(await signatures.GetMineAsync(AgentId, cancellationToken));

    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyList<SignatureDetails>>> Available(CancellationToken cancellationToken) =>
        Ok(await signatures.GetAvailableAsync(AgentId, cancellationToken));

    [HttpPost("{requestId:guid}/offers")]
    public async Task<ActionResult<SignatureDetails>> Offer(Guid requestId, CancellationToken cancellationToken) =>
        Ok(await signatures.OfferAsync(AgentId, requestId, cancellationToken));

    [HttpPost("{requestId:guid}/offers/{offerId:guid}/confirm")]
    public async Task<ActionResult<SignatureDetails>> Confirm(Guid requestId, Guid offerId, CancellationToken cancellationToken) =>
        Ok(await signatures.ConfirmSignerAsync(AgentId, requestId, offerId, cancellationToken));

    [HttpPost("{requestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid requestId, CancellationToken cancellationToken)
    {
        await signatures.CancelAsync(AgentId, requestId, cancellationToken);
        return NoContent();
    }
}
