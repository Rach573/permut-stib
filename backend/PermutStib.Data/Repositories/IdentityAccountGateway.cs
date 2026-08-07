using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Business.Services;
using PermutStib.Data.Entities;

namespace PermutStib.Data.Repositories;

public sealed class IdentityAccountGateway(UserManager<AgentUser> userManager) : IAccountGateway
{
    public async Task<AgentAccount> RegisterAsync(RegisterAgentCommand command, CancellationToken cancellationToken)
    {
        if (await userManager.Users.AnyAsync(x => x.Matricule == command.Matricule, cancellationToken))
            throw new InvalidOperationException("Ce matricule possède déjà un compte.");

        var user = new AgentUser
        {
            Id = Guid.NewGuid(),
            UserName = command.Matricule,
            Matricule = command.Matricule,
            PhoneNumber = command.PhoneNumber,
            Status = AgentStatus.Pending,
            AppRole = AgentRole.Agent
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));

        return ToBusiness(user);
    }

    public async Task<AuthenticatedAgent?> AuthenticateAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var identifier = command.Identifier.Replace(" ", "", StringComparison.Ordinal).Trim();
        var user = await userManager.Users.SingleOrDefaultAsync(
            x => x.Matricule == identifier || x.PhoneNumber == identifier,
            cancellationToken);

        if (user is null || await userManager.IsLockedOutAsync(user))
            return null;

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return new AuthenticatedAgent(user.Id, user.Matricule, user.Status, user.AppRole);
    }

    public async Task SetStatusAsync(Guid agentId, AgentStatus status, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.SingleAsync(x => x.Id == agentId, cancellationToken);
        user.Status = status;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
    }

    public async Task<IReadOnlyList<AgentAccount>> GetPendingAsync(CancellationToken cancellationToken) =>
        await userManager.Users
            .Where(x => x.Status == AgentStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AgentAccount(x.Id, x.Matricule, x.PhoneNumber!, x.Status, x.AppRole, x.CreatedAt))
            .ToListAsync(cancellationToken);

    private static AgentAccount ToBusiness(AgentUser user) =>
        new(user.Id, user.Matricule, user.PhoneNumber!, user.Status, user.AppRole, user.CreatedAt);
}
