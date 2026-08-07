using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Business.Services;
using PermutStib.Data.Entities;
using PermutStib.Data.Persistence;

namespace PermutStib.Data.Repositories;

public sealed class SignatureGateway(PermutStibDbContext db) : ISignatureGateway
{
    public async Task<SignatureDetails> CreateAsync(Guid requesterId, CreateSignatureCommand command, CancellationToken cancellationToken)
    {
        if (await db.Signatures.AnyAsync(x => x.RequesterId == requesterId && x.ServiceDate == command.ServiceDate &&
            x.Status != SignatureStatus.Cancelled, cancellationToken))
            throw new BusinessRuleException("Une demande existe déjà pour cette date.");
        var entity = new SignatureRecord { Id = Guid.NewGuid(), RequesterId = requesterId, ServiceDate = command.ServiceDate, Comment = command.Comment };
        db.Signatures.Add(entity);
        Audit(entity.Id, "Created", requesterId, requesterId, null, entity);
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<SignatureDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken) =>
        (await Query().Where(x => x.RequesterId == agentId || x.Offers.Any(o => o.SignerId == agentId))
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<IReadOnlyList<SignatureDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken) =>
        (await Query().Where(x => x.RequesterId != agentId && x.Status != SignatureStatus.Locked && x.Status != SignatureStatus.Cancelled)
            .OrderBy(x => x.ServiceDate).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<SignatureDetails> OfferAsync(Guid signerId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de signature introuvable.");
        if (request.RequesterId == signerId) throw new BusinessRuleException("Vous ne pouvez pas signer pour vous-même.");
        if (request.Status is SignatureStatus.Locked or SignatureStatus.Cancelled)
            throw new BusinessRuleException("Cette demande n'accepte plus de signataire.");
        if (request.Offers.Any(x => x.SignerId == signerId)) throw new BusinessRuleException("Vous vous êtes déjà proposé.");
        var offer = new SignatureOfferRecord { Id = Guid.NewGuid(), RequestId = request.Id, Request = request, SignerId = signerId };
        request.Offers.Add(offer);
        request.Status = SignatureStatus.ProposalReceived;
        Audit(request.Id, "SignerOffered", signerId, request.RequesterId, null, offer);
        await db.SaveChangesAsync(cancellationToken);
        return Map(request);
    }

    public async Task<SignatureDetails> ConfirmSignerAsync(Guid requesterId, Guid requestId, Guid offerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de signature introuvable.");
        if (request.RequesterId != requesterId) throw new UnauthorizedAccessException("Seul le demandeur peut confirmer un signataire.");
        if (request.Status != SignatureStatus.ProposalReceived) throw new BusinessRuleException("Aucun signataire ne peut être confirmé.");
        var offer = request.Offers.SingleOrDefault(x => x.Id == offerId) ?? throw new KeyNotFoundException("Proposition de signature introuvable.");
        if (await db.Signatures.AnyAsync(x => x.Id != requestId && x.ServiceDate == request.ServiceDate &&
            x.SignerId == offer.SignerId && x.Status == SignatureStatus.Locked, cancellationToken))
            throw new BusinessRuleException("Ce signataire est déjà engagé pour cette date.");
        offer.Status = SignatureOfferStatus.Selected;
        foreach (var other in request.Offers.Where(x => x.Id != offerId && x.Status == SignatureOfferStatus.Pending)) other.Status = SignatureOfferStatus.Rejected;
        request.SignerId = offer.SignerId;
        request.Status = SignatureStatus.Locked;
        request.LockedAt = DateTimeOffset.UtcNow;
        Audit(request.Id, "Locked", requesterId, offer.SignerId, null, new { request.SignerId, request.LockedAt });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(request);
    }

    public async Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.Signatures.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de signature introuvable.");
        if (request.RequesterId != requesterId) throw new UnauthorizedAccessException("Seul le demandeur peut annuler sa demande.");
        if (request.Status == SignatureStatus.Locked) throw new BusinessRuleException("Une signature confirmée ne peut plus être annulée par un agent.");
        request.Status = SignatureStatus.Cancelled;
        Audit(request.Id, "Cancelled", requesterId, requesterId, null, null);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HelpStatistics>> GetHelpStatisticsAsync(CancellationToken cancellationToken)
    {
        var agents = await db.Users.Where(x => x.Status == AgentStatus.Active).Select(x => new { x.Id, x.Matricule }).ToListAsync(cancellationToken);
        var locked = await db.Signatures.Where(x => x.Status == SignatureStatus.Locked).Select(x => new { x.RequesterId, x.SignerId }).ToListAsync(cancellationToken);
        var offers = await db.SignatureOffers.GroupBy(x => x.SignerId).Select(x => new { AgentId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.AgentId, x => x.Count, cancellationToken);
        return agents.Select(a =>
        {
            var received = locked.Count(x => x.RequesterId == a.Id);
            var given = locked.Count(x => x.SignerId == a.Id);
            return new HelpStatistics(a.Id, a.Matricule, received, given, offers.GetValueOrDefault(a.Id), received == 0 ? given : decimal.Round((decimal)given / received, 2));
        }).OrderByDescending(x => x.SignaturesReceived).ToList();
    }

    private IQueryable<SignatureRecord> Query() => db.Signatures.Include(x => x.Offers);
    private static SignatureDetails Map(SignatureRecord x) => new(x.Id, x.RequesterId, x.ServiceDate, x.Comment, x.Status, x.SignerId, x.CreatedAt, x.LockedAt,
        x.Offers.Select(o => new SignatureOffer(o.Id, o.RequestId, o.SignerId, o.Status, o.CreatedAt)).ToList());
    private void Audit(Guid id, string action, Guid actorId, Guid? subjectId, object? before, object? after) => db.AuditLog.Add(new AuditRecord
    {
        EntityType = "Signature", EntityId = id.ToString(), Action = action, ActorId = actorId, SubjectUserId = subjectId,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before), AfterJson = after is null ? null : JsonSerializer.Serialize(after)
    });
}
