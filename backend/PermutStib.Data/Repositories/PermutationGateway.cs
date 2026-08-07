using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Business.Services;
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
        (await Query().Where(x => x.RequesterId != agentId && x.Status == PermutationStatus.Open)
            .OrderBy(x => x.WantedFrom).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<PermutationDetails> ProposeAsync(Guid partnerId, ProposePermutationCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await Query().SingleOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        if (request.RequesterId == partnerId) throw new BusinessRuleException("Vous ne pouvez pas répondre à votre propre demande.");
        if (request.Status is not PermutationStatus.Open and not PermutationStatus.ProposalReceived)
            throw new BusinessRuleException("Cette demande n'accepte plus de proposition.");
        if (command.OfferedPeriod.From != request.WantedFrom || command.OfferedPeriod.To != request.WantedTo)
            throw new BusinessRuleException("La période proposée doit correspondre à la période recherchée.");
        if (request.Proposals.Any(x => x.PartnerId == partnerId))
            throw new BusinessRuleException("Vous avez déjà proposé une période pour cette demande.");

        var proposal = new PermutationProposalRecord
        {
            Id = Guid.NewGuid(), RequestId = request.Id, Request = request, PartnerId = partnerId,
            OfferedFrom = command.OfferedPeriod.From, OfferedTo = command.OfferedPeriod.To
        };
        request.Proposals.Add(proposal);
        request.Status = PermutationStatus.ProposalReceived;
        Audit("Permutation", request.Id, "ProposalCreated", partnerId, request.RequesterId, null, proposal);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(request);
    }

    public async Task<PermutationDetails> AcceptProposalAsync(Guid requesterId, Guid requestId, Guid proposalId, CancellationToken cancellationToken)
    {
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        if (request.RequesterId != requesterId) throw new UnauthorizedAccessException("Seul le demandeur peut accepter une proposition.");
        if (request.Status != PermutationStatus.ProposalReceived) throw new BusinessRuleException("La demande n'est pas en attente d'acceptation.");
        var proposal = request.Proposals.SingleOrDefault(x => x.Id == proposalId)
            ?? throw new KeyNotFoundException("Proposition introuvable.");
        proposal.Status = PermutationProposalStatus.Accepted;
        foreach (var other in request.Proposals.Where(x => x.Id != proposalId && x.Status == PermutationProposalStatus.Pending))
            other.Status = PermutationProposalStatus.Rejected;
        request.AcceptedProposalId = proposalId;
        request.Status = PermutationStatus.Accepted;
        Audit("Permutation", request.Id, "ProposalAccepted", requesterId, proposal.PartnerId, null, new { proposalId });
        await db.SaveChangesAsync(cancellationToken);
        return Map(request);
    }

    public async Task<PermutationDetails> ConfirmAsync(Guid agentId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        if (request.Status is not PermutationStatus.Accepted and not PermutationStatus.Confirmed)
            throw new BusinessRuleException("Cette permutation ne peut pas être confirmée.");
        var accepted = request.Proposals.Single(x => x.Id == request.AcceptedProposalId);
        if (agentId == request.RequesterId) request.RequesterConfirmed = true;
        else if (agentId == accepted.PartnerId) request.PartnerConfirmed = true;
        else throw new UnauthorizedAccessException("Vous ne participez pas à cette permutation.");

        if (request.RequesterConfirmed && request.PartnerConfirmed)
        {
            var conflict = await db.Permutations.AnyAsync(x => x.Id != request.Id && x.Status == PermutationStatus.Locked &&
                ((x.RequesterId == request.RequesterId && x.OwnedFrom <= request.OwnedTo && request.OwnedFrom <= x.OwnedTo) ||
                 (x.Proposals.Any(p => p.Id == x.AcceptedProposalId && p.PartnerId == accepted.PartnerId) &&
                  x.Proposals.Any(p => p.Id == x.AcceptedProposalId && p.OfferedFrom <= accepted.OfferedTo && accepted.OfferedFrom <= p.OfferedTo))), cancellationToken);
            if (conflict) throw new BusinessRuleException("Une des périodes est déjà engagée dans une permutation verrouillée.");
            request.Status = PermutationStatus.Locked;
            request.LockedAt = DateTimeOffset.UtcNow;
        }
        else request.Status = PermutationStatus.Confirmed;

        Audit("Permutation", request.Id, request.Status == PermutationStatus.Locked ? "Locked" : "Confirmed", agentId, request.RequesterId, null, new { request.RequesterConfirmed, request.PartnerConfirmed });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(request);
    }

    public async Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.Permutations.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de permutation introuvable.");
        if (request.RequesterId != requesterId) throw new UnauthorizedAccessException("Seul le demandeur peut annuler sa demande.");
        if (request.Status is PermutationStatus.Confirmed or PermutationStatus.Locked)
            throw new BusinessRuleException("Une permutation confirmée ne peut plus être annulée par un agent.");
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
}
