using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace PermutStib.Api.Controllers;

public abstract class AuthenticatedController : ControllerBase
{
    protected Guid AgentId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new UnauthorizedAccessException("Session invalide.");
}
