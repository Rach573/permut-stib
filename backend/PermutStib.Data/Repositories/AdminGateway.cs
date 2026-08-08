using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Business.Services;
using PermutStib.Business.Rules;
using PermutStib.Data.Entities;
using PermutStib.Data.Persistence;

namespace PermutStib.Data.Repositories;

public sealed class AdminGateway(PermutStibDbContext db) : IAdminGateway
{
    public async Task<AdminSummary> GetSummaryAsync(CancellationToken token) => new(
        await db.Users.CountAsync(x => x.Status == AgentStatus.Pending, token),
        await db.Users.CountAsync(x => x.AppRole == AgentRole.Agent && x.Status == AgentStatus.Active, token),
        await db.Users.CountAsync(x => x.Status == AgentStatus.Suspended, token),
        await db.Permutations.CountAsync(x => x.Status == PermutationStatus.Open || x.Status == PermutationStatus.ProposalReceived, token),
        await db.Permutations.CountAsync(x => x.Status == PermutationStatus.Confirmed || x.Status == PermutationStatus.Locked, token),
        await db.Signatures.CountAsync(x => x.Status == SignatureStatus.Open || x.Status == SignatureStatus.ProposalReceived, token),
        await db.Signatures.CountAsync(x => x.Status == SignatureStatus.Confirmed || x.Status == SignatureStatus.Locked, token),
        await db.AuditLog.CountAsync(token));

    public async Task<IReadOnlyList<AdminAgent>> GetAgentsAsync(CancellationToken token) =>
        await db.Users.AsNoTracking().OrderBy(x => x.Matricule)
            .Select(x => new AdminAgent(x.Id, x.Matricule, x.PhoneNumber!, x.Status, x.AppRole, x.CreatedAt))
            .ToListAsync(token);

    public async Task SetAgentStatusAsync(Guid actorId, Guid agentId, AgentStatus status, string? reason, CancellationToken token)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == agentId, token)
            ?? throw new KeyNotFoundException("Agent introuvable.");
        reason = AdminRules.ValidateStatusChange(user.AppRole, status, reason);
        var before = user.Status;
        if (before == status) return;
        user.Status = status;
        db.AuditLog.Add(new AuditRecord
        {
            ActorId = actorId, SubjectUserId = user.Id, EntityType = "Agent", EntityId = user.Id.ToString(),
            Action = status switch { AgentStatus.Active => "Activated", AgentStatus.Suspended => "Suspended", _ => "Rejected" },
            BeforeJson = JsonSerializer.Serialize(new { Status = before }),
            AfterJson = JsonSerializer.Serialize(new { Status = status }), Reason = reason
        });
        await db.SaveChangesAsync(token);
    }

    public async Task<IReadOnlyList<AdminPermutation>> GetPermutationsAsync(CancellationToken token) =>
        await db.Permutations.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Join(db.Users, p => p.RequesterId, u => u.Id, (p, u) => new AdminPermutation(
                p.Id, u.Matricule, p.OwnedFrom, p.OwnedTo, p.WantedFrom, p.WantedTo,
                p.Status, p.Proposals.Count, p.CreatedAt)).ToListAsync(token);

    public async Task<IReadOnlyList<AdminSignature>> GetSignaturesAsync(CancellationToken token)
    {
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Matricule, token);
        var rows = await db.Signatures.AsNoTracking().Include(x => x.Offers).OrderByDescending(x => x.CreatedAt).ToListAsync(token);
        return rows.Select(x => new AdminSignature(x.Id, users[x.RequesterId], x.ServiceDate, x.Comment, x.Status,
            x.SignerId is Guid signer && users.TryGetValue(signer, out var matricule) ? matricule : null,
            x.Offers.Count, x.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<HelpStatistics>> GetHelpStatisticsAsync(CancellationToken token)
    {
        var agents = await db.Users.AsNoTracking().Where(x => x.AppRole == AgentRole.Agent).OrderBy(x => x.Matricule).ToListAsync(token);
        var received = await db.Signatures.Where(x => x.Status == SignatureStatus.Locked).GroupBy(x => x.RequesterId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, token);
        var given = await db.Signatures.Where(x => x.Status == SignatureStatus.Locked && x.SignerId != null).GroupBy(x => x.SignerId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, token);
        var offers = await db.SignatureOffers.GroupBy(x => x.SignerId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, token);
        return agents.Select(x =>
        {
            var r = received.GetValueOrDefault(x.Id); var g = given.GetValueOrDefault(x.Id); var o = offers.GetValueOrDefault(x.Id);
            return new HelpStatistics(x.Id, x.Matricule, r, g, o, r == 0 ? null : Math.Round((decimal)g / r, 2));
        }).ToList();
    }

    public async Task<IReadOnlyList<AdminAuditEntry>> GetAuditAsync(CancellationToken token)
    {
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Matricule, token);
        var rows = await db.AuditLog.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(token);
        return rows.Select(x => new AdminAuditEntry(x.Id, x.EntityType, x.EntityId, x.Action,
            x.ActorId is Guid actor && users.TryGetValue(actor, out var actorName) ? actorName : null,
            x.SubjectUserId is Guid subject && users.TryGetValue(subject, out var subjectName) ? subjectName : null,
            x.BeforeJson, x.AfterJson, x.Reason, x.CreatedAt)).ToList();
    }
}
