using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Business.Rules;

public static class AdminRules
{
    public static string ValidateStatusChange(AgentRole targetRole, AgentStatus status, string? reason)
    {
        if (targetRole == AgentRole.Admin)
            throw new BusinessRuleException("Le compte administrateur ne peut pas être modifié ici.");
        if (status is not (AgentStatus.Active or AgentStatus.Suspended or AgentStatus.Rejected))
            throw new ArgumentException("Statut administratif invalide.");
        return string.IsNullOrWhiteSpace(reason)
            ? "Action administrative depuis le tableau de bord"
            : reason.Trim();
    }
}
