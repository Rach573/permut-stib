using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Business.Services;
using PermutStib.Business.Rules;
using PermutStib.Data.Entities;
using PermutStib.Data.Persistence;

namespace PermutStib.Data.Repositories;

public sealed class SignatureGateway(PermutStibDbContext db) : ISignatureGateway
{
    public async Task<SignatureDetails> CreateAsync(Guid requesterId, CreateSignatureCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        SignatureRules.EnsureNoDuplicate(await db.Signatures.AnyAsync(x => x.RequesterId == requesterId && x.ServiceDate == command.ServiceDate &&
            x.Status != SignatureStatus.Cancelled, cancellationToken));
        var entity = new SignatureRecord { Id = Guid.NewGuid(), RequesterId = requesterId, ServiceDate = command.ServiceDate, Comment = command.Comment };
        db.Signatures.Add(entity);

        var matchingAvailabilities = await db.SignatureAvailabilities
            .Where(x => x.IsActive && x.ServiceDate == command.ServiceDate && x.AgentId != requesterId &&
                !db.Signatures.Any(s => s.ServiceDate == command.ServiceDate && s.SignerId == x.AgentId && s.Status == SignatureStatus.Locked))
            .ToListAsync(cancellationToken);
        foreach (var availability in matchingAvailabilities)
        {
            entity.Offers.Add(new SignatureOfferRecord
            {
                Id = Guid.NewGuid(), Request = entity, RequestId = entity.Id, SignerId = availability.AgentId,
                AvailabilityId = availability.Id
            });
            Notify(availability.AgentId, NotificationType.SignatureRequestMatched,
                "Une demande de signature correspond à l'une de vos disponibilités.", entity.Id);
        }
        if (matchingAvailabilities.Count > 0)
        {
            entity.Status = SignatureStatus.ProposalReceived;
            Notify(requesterId, NotificationType.SignatureAvailabilityMatched,
                $"{matchingAvailabilities.Count} agent(s) disponible(s) correspondent déjà à votre demande de signature.", entity.Id);
        }
        Audit(entity.Id, "Created", requesterId, requesterId, null, new
        {
            entity.Id,
            entity.RequesterId,
            entity.ServiceDate,
            entity.Comment,
            entity.Status,
            OfferCount = entity.Offers.Count
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<SignatureDetails>> GetMineAsync(Guid agentId, CancellationToken cancellationToken) =>
        (await Query().Where(x => x.RequesterId == agentId || x.Offers.Any(o => o.SignerId == agentId))
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<IReadOnlyList<SignatureDetails>> GetAvailableAsync(Guid agentId, CancellationToken cancellationToken) =>
        (await Query().Where(x => x.RequesterId != agentId && x.Status != SignatureStatus.Locked && x.Status != SignatureStatus.Cancelled)
            .Where(x => x.Offers.All(o => o.SignerId != agentId))
            .OrderBy(x => x.ServiceDate).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<SignatureDetails> OfferAsync(Guid signerId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de signature introuvable.");
        SignatureRules.EnsureCanOffer(Map(request), signerId);
        var offer = new SignatureOfferRecord { Id = Guid.NewGuid(), RequestId = request.Id, Request = request, SignerId = signerId, AvailabilityId = null };
        db.SignatureOffers.Add(offer);
        request.Status = SignatureStatus.ProposalReceived;
        Notify(request.RequesterId, NotificationType.SignatureOfferReceived, "Un agent se propose pour signer à votre place.", request.Id);
        Audit(request.Id, "SignerOffered", signerId, request.RequesterId, null,
            new { offer.Id, offer.RequestId, offer.SignerId, offer.Status, offer.CreatedAt });
        await db.SaveChangesAsync(cancellationToken);
        return Map(request);
    }

    public async Task<SignatureDetails> ConfirmSignerAsync(Guid requesterId, Guid requestId, Guid offerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de signature introuvable.");
        SignatureRules.EnsureCanConfirm(Map(request), requesterId, offerId);
        var offer = request.Offers.Single(x => x.Id == offerId);
        SignatureRules.EnsureSignerAvailable(await db.Signatures.AnyAsync(x => x.Id != requestId && x.ServiceDate == request.ServiceDate &&
            x.SignerId == offer.SignerId && x.Status == SignatureStatus.Locked, cancellationToken));
        offer.Status = SignatureOfferStatus.Selected;
        foreach (var other in request.Offers.Where(x => x.Id != offerId && x.Status == SignatureOfferStatus.Pending)) other.Status = SignatureOfferStatus.Rejected;
        request.SignerId = offer.SignerId;
        request.Status = SignatureStatus.Locked;
        request.LockedAt = DateTimeOffset.UtcNow;
        if (offer.AvailabilityId is Guid availabilityId)
        {
            var availability = await db.SignatureAvailabilities.SingleOrDefaultAsync(x => x.Id == availabilityId, cancellationToken);
            if (availability is not null) availability.IsActive = false;
        }

        var competingOffers = await db.SignatureOffers.Include(x => x.Request).ThenInclude(x => x.Offers)
            .Where(x => x.RequestId != requestId && x.SignerId == offer.SignerId && x.Status == SignatureOfferStatus.Pending &&
                x.Request.ServiceDate == request.ServiceDate)
            .ToListAsync(cancellationToken);
        foreach (var competing in competingOffers)
        {
            competing.Status = SignatureOfferStatus.Rejected;
            if (competing.Request.SignerId is null && competing.Request.Offers.All(x => x.Id == competing.Id || x.Status != SignatureOfferStatus.Pending))
                competing.Request.Status = SignatureStatus.Open;
        }
        Notify(offer.SignerId, NotificationType.SignatureOfferAccepted, "Vous avez été choisi comme signataire. L'engagement est désormais verrouillé.", request.Id);
        Audit(request.Id, "Locked", requesterId, offer.SignerId, null, new { request.SignerId, request.LockedAt });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(request);
    }

    public async Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await Query().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Demande de signature introuvable.");
        SignatureRules.EnsureCanCancel(Map(request), requesterId);
        request.Status = SignatureStatus.Cancelled;
        Audit(request.Id, "Cancelled", requesterId, requesterId, null, null);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SignatureAvailability> CreateAvailabilityAsync(Guid agentId, CreateSignatureAvailabilityCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var entity = await db.SignatureAvailabilities.SingleOrDefaultAsync(
            x => x.AgentId == agentId && x.ServiceDate == command.ServiceDate, cancellationToken);
        if (entity?.IsActive == true)
            throw new BusinessRuleException("Vous êtes déjà disponible pour cette date.");

        if (entity is null)
        {
            entity = new SignatureAvailabilityRecord
            {
                Id = Guid.NewGuid(), AgentId = agentId, ServiceDate = command.ServiceDate, Comment = command.Comment
            };
            db.SignatureAvailabilities.Add(entity);
        }
        else
        {
            entity.Comment = command.Comment;
            entity.IsActive = true;
            entity.CreatedAt = DateTimeOffset.UtcNow;
        }

        var matchingRequests = await Query()
            .Where(x => x.RequesterId != agentId && x.ServiceDate == command.ServiceDate &&
                (x.Status == SignatureStatus.Open || x.Status == SignatureStatus.ProposalReceived) &&
                x.Offers.All(o => o.SignerId != agentId))
            .ToListAsync(cancellationToken);
        foreach (var request in matchingRequests)
        {
            request.Offers.Add(new SignatureOfferRecord
            {
                Id = Guid.NewGuid(), Request = request, RequestId = request.Id, SignerId = agentId, AvailabilityId = entity.Id
            });
            request.Status = SignatureStatus.ProposalReceived;
            Notify(request.RequesterId, NotificationType.SignatureAvailabilityMatched,
                "Un agent disponible correspond à votre demande de signature.", request.Id);
            Notify(agentId, NotificationType.SignatureRequestMatched,
                "Une demande de signature correspond à la disponibilité que vous venez de proposer.", request.Id);
        }

        AuditAvailability(entity.Id, "Created", agentId, null, entity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<SignatureAvailability>> GetMyAvailabilitiesAsync(Guid agentId, CancellationToken cancellationToken) =>
        await db.SignatureAvailabilities.Where(x => x.AgentId == agentId)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.ServiceDate)
            .Select(x => new SignatureAvailability(x.Id, x.AgentId, x.ServiceDate, x.Comment, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task CancelAvailabilityAsync(Guid agentId, Guid availabilityId, CancellationToken cancellationToken)
    {
        var availability = await db.SignatureAvailabilities.SingleOrDefaultAsync(x => x.Id == availabilityId && x.AgentId == agentId, cancellationToken)
            ?? throw new KeyNotFoundException("Disponibilité de signature introuvable.");
        availability.IsActive = false;

        var proactiveOffers = await db.SignatureOffers.Include(x => x.Request).ThenInclude(x => x.Offers)
            .Where(x => x.AvailabilityId == availabilityId && x.Status == SignatureOfferStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var offer in proactiveOffers)
        {
            offer.Status = SignatureOfferStatus.Withdrawn;
            if (offer.Request.SignerId is null && offer.Request.Offers.All(x => x.Id == offer.Id || x.Status != SignatureOfferStatus.Pending))
                offer.Request.Status = SignatureStatus.Open;
        }

        AuditAvailability(availability.Id, "Cancelled", agentId, availability, null);
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
        x.Offers.Select(o => new SignatureOffer(o.Id, o.RequestId, o.SignerId, o.AvailabilityId, o.Status, o.CreatedAt)).ToList());
    private static SignatureAvailability Map(SignatureAvailabilityRecord x) =>
        new(x.Id, x.AgentId, x.ServiceDate, x.Comment, x.IsActive, x.CreatedAt);
    private void Audit(Guid id, string action, Guid actorId, Guid? subjectId, object? before, object? after) => db.AuditLog.Add(new AuditRecord
    {
        EntityType = "Signature", EntityId = id.ToString(), Action = action, ActorId = actorId, SubjectUserId = subjectId,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before), AfterJson = after is null ? null : JsonSerializer.Serialize(after)
    });
    private void Notify(Guid recipientId, NotificationType type, string message, Guid entityId) => db.Notifications.Add(new NotificationRecord
    {
        Id = Guid.NewGuid(), RecipientId = recipientId, Type = type, Message = message, EntityType = "Signature", EntityId = entityId
    });
    private void AuditAvailability(Guid id, string action, Guid actorId, object? before, object? after) => db.AuditLog.Add(new AuditRecord
    {
        EntityType = "SignatureAvailability", EntityId = id.ToString(), Action = action, ActorId = actorId, SubjectUserId = actorId,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before), AfterJson = after is null ? null : JsonSerializer.Serialize(after)
    });
}
