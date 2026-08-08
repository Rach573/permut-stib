using System.Text.RegularExpressions;
using PermutStib.Business.Models;

namespace PermutStib.Business.Services;

public sealed partial class AccountService(IAccountGateway gateway)
{
    public Task<AgentAccount> RegisterAsync(RegisterAgentCommand command, CancellationToken cancellationToken)
    {
        var matricule = NormalizeMatricule(command.Matricule);
        var phone = NormalizePhone(command.PhoneNumber);

        if (matricule.Length < 3)
            throw new ArgumentException("Le matricule est invalide.");

        if (!BelgianPhoneRegex().IsMatch(phone))
            throw new ArgumentException("Le numéro de GSM est invalide.");

        if (command.Password.Length < 8)
            throw new ArgumentException("Le mot de passe doit contenir au moins 8 caractères.");

        return gateway.RegisterAsync(command with { Matricule = matricule, PhoneNumber = phone }, cancellationToken);
    }

    public async Task<AuthenticatedAgent?> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var account = await gateway.AuthenticateAsync(command, cancellationToken);
        return account is { Status: AgentStatus.Active } ? account : null;
    }

    public Task<IReadOnlyList<AgentAccount>> GetPendingAsync(CancellationToken cancellationToken) =>
        gateway.GetPendingAsync(cancellationToken);

    public Task ApproveAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.SetStatusAsync(agentId, AgentStatus.Active, cancellationToken);

    public Task RejectAsync(Guid agentId, CancellationToken cancellationToken) =>
        gateway.SetStatusAsync(agentId, AgentStatus.Rejected, cancellationToken);

    private static string NormalizeMatricule(string value) => value.Replace(" ", "", StringComparison.Ordinal).Trim();
    private static string NormalizePhone(string value) => value.Replace(" ", "", StringComparison.Ordinal).Trim();

    [GeneratedRegex(@"^(\+32|0)4\d{8}$")]
    private static partial Regex BelgianPhoneRegex();
}
