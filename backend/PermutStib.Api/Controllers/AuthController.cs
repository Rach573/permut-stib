using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AccountService accounts) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        matricule = User.FindFirstValue("matricule"),
        role = User.FindFirstValue("app_role")
    });

    [HttpPost("register")]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Register(RegisterAgentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var account = await accounts.RegisterAsync(command, cancellationToken);
            return Accepted(new { account.Id, account.Matricule, account.Status });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.LoginAsync(command, cancellationToken);
        if (account is null)
            return Unauthorized(new { error = "Identifiants invalides ou compte non actif." });

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim("matricule", account.Matricule),
            new Claim("app_role", account.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Ok(new
        {
            id = account.Id,
            matricule = account.Matricule,
            role = account.Role.ToString()
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
