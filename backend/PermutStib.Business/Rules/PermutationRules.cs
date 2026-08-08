using PermutStib.Business.Models;
using PermutStib.Business.Services;

namespace PermutStib.Business.Rules;

public enum PermutationParticipant
{
    Requester,
    Partner
}

public sealed record PermutationConfirmationDecision(
    PermutationParticipant Participant,
    bool RequesterConfirmed,
    bool PartnerConfirmed,
    PermutationStatus Status,
    Guid? NotificationRecipientId);

public static class PermutationRules
{
    public static void ValidateCreation(CreatePermutationCommand command, DateOnly today)
    {
        ValidatePeriod(command.OwnedPeriod, "possédée", today);
        ValidatePeriod(command.WantedPeriod, "recherchée", today);
        if (command.OwnedPeriod.Overlaps(command.WantedPeriod))
            throw new BusinessRuleException("Les périodes possédée et recherchée ne peuvent pas se chevaucher.");
    }

    public static void ValidateProposalPeriod(DatePeriod period, DateOnly today) =>
        ValidatePeriod(period, "proposée", today);

    public static void EnsureCanPropose(PermutationDetails request, Guid partnerId, DatePeriod offeredPeriod)
    {
        if (request.RequesterId == partnerId)
            throw new BusinessRuleException("Vous ne pouvez pas répondre à votre propre demande.");
        if (request.Status is not PermutationStatus.Open and not PermutationStatus.ProposalReceived)
            throw new BusinessRuleException("Cette demande n'accepte plus de proposition.");
        if (offeredPeriod != request.WantedPeriod)
            throw new BusinessRuleException("La période proposée doit correspondre à la période recherchée.");
        if (request.Proposals.Any(x => x.PartnerId == partnerId))
            throw new BusinessRuleException("Vous avez déjà proposé une période pour cette demande.");
    }

    public static void EnsureCanAccept(PermutationDetails request, Guid requesterId, Guid proposalId)
    {
        if (request.RequesterId != requesterId)
            throw new UnauthorizedAccessException("Seul le demandeur peut accepter une proposition.");
        if (request.Status != PermutationStatus.ProposalReceived)
            throw new BusinessRuleException("La demande n'est pas en attente d'acceptation.");
        if (request.Proposals.All(x => x.Id != proposalId))
            throw new KeyNotFoundException("Proposition introuvable.");
    }

    public static PermutationConfirmationDecision DecideConfirmation(PermutationDetails request, Guid agentId)
    {
        if (request.Status is not PermutationStatus.Accepted and not PermutationStatus.Confirmed)
            throw new BusinessRuleException("Cette permutation ne peut pas être confirmée.");

        var accepted = request.Proposals.SingleOrDefault(x => x.Id == request.AcceptedProposalId)
            ?? throw new BusinessRuleException("La proposition acceptée est introuvable.");
        var participant = agentId == request.RequesterId
            ? PermutationParticipant.Requester
            : agentId == accepted.PartnerId
                ? PermutationParticipant.Partner
                : throw new UnauthorizedAccessException("Vous ne participez pas à cette permutation.");

        var requesterConfirmed = request.RequesterConfirmed || participant == PermutationParticipant.Requester;
        var partnerConfirmed = request.PartnerConfirmed || participant == PermutationParticipant.Partner;
        var locked = requesterConfirmed && partnerConfirmed;
        Guid? recipient = locked
            ? null
            : participant == PermutationParticipant.Requester ? accepted.PartnerId : request.RequesterId;

        return new(participant, requesterConfirmed, partnerConfirmed,
            locked ? PermutationStatus.Locked : PermutationStatus.Confirmed, recipient);
    }

    public static void EnsureNoLockedConflict(bool conflict)
    {
        if (conflict)
            throw new BusinessRuleException("Une des périodes est déjà engagée dans une permutation verrouillée.");
    }

    public static void EnsureCanCancel(PermutationDetails request, Guid requesterId)
    {
        if (request.RequesterId != requesterId)
            throw new UnauthorizedAccessException("Seul le demandeur peut annuler sa demande.");
        if (request.Status is PermutationStatus.Confirmed or PermutationStatus.Locked)
            throw new BusinessRuleException("Une permutation confirmée ne peut plus être annulée par un agent.");
    }

    private static void ValidatePeriod(DatePeriod period, string label, DateOnly today)
    {
        if (period.To < period.From)
            throw new ArgumentException($"La période {label} est invalide.");
        if (period.From < today)
            throw new ArgumentException($"La période {label} ne peut pas commencer dans le passé.");
    }
}
