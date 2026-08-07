using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationService notifications) : AuthenticatedController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentNotification>>> Mine([FromQuery] bool unreadOnly, CancellationToken cancellationToken) =>
        Ok(await notifications.GetMineAsync(AgentId, unreadOnly, cancellationToken));

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(AgentId, notificationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(AgentId, cancellationToken);
        return NoContent();
    }
}
