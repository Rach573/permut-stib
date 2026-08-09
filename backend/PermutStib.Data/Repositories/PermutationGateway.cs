using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Business.Services;
using PermutStib.Business.Rules;
using PermutStib.Data.Entities;
using PermutStib.Data.Persistence;

namespace PermutStib.Data.Repositories;

public sealed class PermutationGateway(PermutStibDbContext db) : IPermutationGateway
{
    public async Task<PermutationDetails> CreateAsync(Guid requesterId, CreatePermutationCommand command, CancellationToken cancellationToken)
    {
        var entity = new PermutationRecord
        {
            Id = Guid.NewGuid(), RequesterId = requesterId,
            OwnedFrom = command.OwnedPeriod.From, OwnedTo = command.OwnedPeriod.To,
            WantedFrom = command.WantedPeriod.From, WantedTo = command.WantedPeriod.To
        };
        db.Permutations.Add(entity);
        Audit("Permutation", entity.Id, "Created", requesterId, requesterId, null, entity);
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<PermutationDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken) =>
        (await Query().Where(x => x.RequesterId == agentId || x.Proposals.Any(p => p.PartnerId == agentId))
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<IReadOnlyList<PermutationDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken) =>
        (await Query().Where(x => x.RequesterId != agentId &&
                (x.Status == PermutationStatus.Open || x.Status == PermutationStatus.ProposalReceived) &&
                x.Proposals.All(p => p.PartnerId != agentId))
            .OrderBy(x => x.WantedFrom).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<PermutationDetails> ProposeAsync(Guid partnerId, ProposePermutationCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await Query().SingleOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        PermutationRules.EnsureCanPropose(Map(request), partnerId, command.OfferedPeriod);

        var proposal = new PermutationProposalRecord
        {
            Id = Guid.NewGuid(), RequestId = request.Id, Request = request, PartnerId = partnerId,
            OfferedFrom = command.OfferedPeriod.From, OfferedTo = command.OfferedPeriod.To
        };
        db.PermutationProposals.Add(proposal);
        request.Status = PermutationStatus.ProposalReceived;
        Notify(request.RequesterId, NotificationType.PermutationProposalReceived, "Nouvelle proposition de permutation.", request.Id);
        Audit("Permutation", request.Id, "ProposalCreated", partnerId, request.RequesterId, null,
            new { proposal.Id, proposal.RequestId, proposal.PartnerId, proposal.OfferedFrom, proposal.OfferedTo, proposal.Status });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(request);
    }

    public async Task<PermutationDetails> AcceptProposalAsync(Guid requesterId, Guid requestId, Guid proposalId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        PermutationRules.EnsureCanAccept(Map(request), requesterId, proposalId);
        var proposal = request.Proposals.SingleOrDefault(x => x.Id == proposalId)
            ?? throw new KeyNotFoundException("Proposition introuvable.");
        proposal.Status = PermutationProposalStatus.Accepted;
        foreach (var other in request.Proposals.Where(x => x.Id != proposalId && x.Status == PermutationProposalStatus.Pending))
            other.Status = PermutationProposalStatus.Rejected;
        request.AcceptedProposalId = proposalId;
        request.Status = PermutationStatus.Accepted;
        Notify(proposal.PartnerId, NotificationType.PermutationProposalAccepted, "Proposition acceptée : confirmez l’échange.", request.Id);
        Audit("Permutation", request.Id, "ProposalAccepted", requesterId, proposal.PartnerId, null, new { proposalId });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(request);
    }

    public async Task<PermutationDetails> ConfirmAsync(Guid agentId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        var accepted = request.Proposals.Single(x => x.Id == request.AcceptedProposalId);
        var decision = PermutationRules.DecideConfirmation(Map(request), agentId);
        request.RequesterConfirmed = decision.RequesterConfirmed;
        request.PartnerConfirmed = decision.PartnerConfirmed;

        if (decision.Status == PermutationStatus.Locked)
        {
            var conflict = await db.Permutations.AnyAsync(x => x.Id != request.Id && x.Status == PermutationStatus.Locked &&
                ((x.RequesterId == request.RequesterId && x.OwnedFrom <= request.OwnedTo && request.OwnedFrom <= x.OwnedTo) ||
                 (x.Proposals.Any(p => p.Id == x.AcceptedProposalId && p.PartnerId == accepted.PartnerId) &&
                  x.Proposals.Any(p => p.Id == x.AcceptedProposalId && p.OfferedFrom <= accepted.OfferedTo && accepted.OfferedFrom <= p.OfferedTo))), cancellationToken);
            PermutationRules.EnsureNoLockedConflict(conflict);
            request.Status = PermutationStatus.Locked;
            request.LockedAt = DateTimeOffset.UtcNow;
            Notify(request.RequesterId, NotificationType.PermutationLocked, "Permutation confirmée.", request.Id);
            Notify(accepted.PartnerId, NotificationType.PermutationLocked, "Permutation confirmée.", request.Id);
        }
        else
        {
            request.Status = PermutationStatus.Confirmed;
            Notify(decision.NotificationRecipientId!.Value, NotificationType.PermutationConfirmed, "Le collègue a confirmé. À votre tour.", request.Id);
        }

        Audit("Permutation", request.Id, request.Status == PermutationStatus.Locked ? "Locked" : "Confirmed", agentId, request.RequesterId, null, new { request.RequesterConfirmed, request.PartnerConfirmed });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(request);
    }

    public async Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        PermutationRules.EnsureCanCancel(Map(request), requesterId);
        request.Status = PermutationStatus.Cancelled;
        Audit("Permutation", request.Id, "Cancelled", requesterId, requesterId, null, null);
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<PermutationRecord> Query() => db.Permutations.Include(x => x.Proposals);

    private static PermutationDetails Map(PermutationRecord x) => new(
        x.Id, x.RequesterId, new(x.OwnedFrom, x.OwnedTo), new(x.WantedFrom, x.WantedTo), x.Status,
        x.AcceptedProposalId, x.RequesterConfirmed, x.PartnerConfirmed, x.CreatedAt, x.LockedAt,
        x.Proposals.Select(p => new PermutationProposal(p.Id, p.RequestId, p.PartnerId, new(p.OfferedFrom, p.OfferedTo), p.Status, p.CreatedAt)).ToList());

    private void Audit(string type, Guid id, string action, Guid actorId, Guid? subjectId, object? before, object? after) =>
        db.AuditLog.Add(new AuditRecord { EntityType = type, EntityId = id.ToString(), Action = action, ActorId = actorId,
            SubjectUserId = subjectId, BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after) });

    private void Notify(Guid recipientId, NotificationType type, string message, Guid entityId) =>
        db.Notifications.Add(new NotificationRecord { Id = Guid.NewGuid(), RecipientId = recipientId, Type = type,
            Message = message, EntityType = "Permutation", EntityId = entityId });
}
