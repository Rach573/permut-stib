using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Business.Rules;

public static class SignatureRules
{
    public static CreateSignatureCommand ValidateCreation(CreateSignatureCommand command, DateOnly today)
    {
        if (command.ServiceDate < today)
            throw new ArgumentException("La date de signature ne peut pas être dans le passé.");
        if (command.Comment?.Length > 500)
            throw new ArgumentException("Le commentaire ne peut pas dépasser 500 caractères.");
        return command with { Comment = command.Comment?.Trim() };
    }

    public static void EnsureNoDuplicate(bool duplicate)
    {
        if (duplicate)
            throw new BusinessRuleException("Une demande existe déjà pour cette date.");
    }

    public static void EnsureCanOffer(SignatureDetails request, Guid signerId)
    {
        if (request.RequesterId == signerId)
            throw new BusinessRuleException("Vous ne pouvez pas signer pour vous-même.");
        if (request.Status is SignatureStatus.Locked or SignatureStatus.Cancelled)
            throw new BusinessRuleException("Cette demande n'accepte plus de signataire.");
        if (request.Offers.Any(x => x.SignerId == signerId))
            throw new BusinessRuleException("Vous vous êtes déjà proposé.");
    }

    public static SignatureOffer EnsureCanConfirm(SignatureDetails request, Guid requesterId, Guid offerId)
    {
        if (request.RequesterId != requesterId)
            throw new UnauthorizedAccessException("Seul le demandeur peut confirmer un signataire.");
        if (request.Status != SignatureStatus.ProposalReceived)
            throw new BusinessRuleException("Aucun signataire ne peut être confirmé.");
        return request.Offers.SingleOrDefault(x => x.Id == offerId)
            ?? throw new KeyNotFoundException("Proposition de signature introuvable.");
    }

    public static void EnsureSignerAvailable(bool alreadyLocked)
    {
        if (alreadyLocked)
            throw new BusinessRuleException("Ce signataire est déjà engagé pour cette date.");
    }

    public static void EnsureCanCancel(SignatureDetails request, Guid requesterId)
    {
        if (request.RequesterId != requesterId)
            throw new UnauthorizedAccessException("Seul le demandeur peut annuler sa demande.");
        if (request.Status == SignatureStatus.Locked)
            throw new BusinessRuleException("Une signature confirmée ne peut plus être annulée par un agent.");
    }
}
