using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermutStib.Business.Models;
using PermutStib.Data.Entities;
using PermutStib.Data.Persistence;

namespace PermutStib.Api;

public static class DemoDataSeeder
{
    private const string DemoPassword = "test1234";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("DemoData:Enabled")) return;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PermutStibDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AgentUser>>();
        if (await users.Users.AnyAsync(x => x.Matricule == "70-001"))
        {
            await ResetDemoPasswordsAsync(users);
            await PrintCountsAsync(db);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var admin = await users.Users.SingleOrDefaultAsync(x => x.Matricule == "DELEGUE")
            ?? await CreateUserAsync(users, "DELEGUE", "+32479000000", AgentStatus.Active, AgentRole.Admin);
        var agents = new List<AgentUser>();
        for (var index = 1; index <= 50; index++)
        {
            var status = index switch
            {
                <= 42 => AgentStatus.Active,
                <= 46 => AgentStatus.Pending,
                <= 48 => AgentStatus.Suspended,
                _ => AgentStatus.Rejected
            };
            agents.Add(await CreateUserAsync(users, $"70-{index:000}", $"+32470{index:000000}", status, AgentRole.Agent));
        }

        var active = agents.Where(x => x.Status == AgentStatus.Active).ToList();
        for (var index = 0; index < 12; index++)
        {
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30 + index * 4));
            var wanted = start.AddMonths(1);
            var request = new PermutationRecord
            {
                Id = Guid.NewGuid(), RequesterId = active[index].Id,
                OwnedFrom = start, OwnedTo = start.AddDays(6), WantedFrom = wanted, WantedTo = wanted.AddDays(6),
                Status = index < 5 ? PermutationStatus.Open : index < 8 ? PermutationStatus.ProposalReceived : PermutationStatus.Locked,
                CreatedAt = now.AddDays(-index - 1), LockedAt = index >= 8 ? now.AddHours(-index) : null
            };
            if (index >= 5)
            {
                var proposal = new PermutationProposalRecord
                {
                    Id = Guid.NewGuid(), Request = request, RequestId = request.Id, PartnerId = active[index + 15].Id,
                    OfferedFrom = wanted, OfferedTo = wanted.AddDays(6),
                    Status = index >= 8 ? PermutationProposalStatus.Accepted : PermutationProposalStatus.Pending,
                    CreatedAt = now.AddDays(-index)
                };
                request.Proposals.Add(proposal);
                if (index >= 8)
                {
                    request.AcceptedProposalId = proposal.Id;
                    request.RequesterConfirmed = request.PartnerConfirmed = true;
                }
            }
            db.Permutations.Add(request);
            AddAudit(db, "Permutation", request.Id, "DemoCreated", admin.Id, request.RequesterId,
                new { request.OwnedFrom, request.OwnedTo, request.WantedFrom, request.WantedTo, request.Status });
        }

        for (var index = 0; index < 14; index++)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10 + index));
            var request = new SignatureRecord
            {
                Id = Guid.NewGuid(), RequesterId = active[index + 20].Id, ServiceDate = date,
                Comment = index % 2 == 0 ? "Service du matin" : null,
                Status = index < 6 ? SignatureStatus.Open : index < 9 ? SignatureStatus.ProposalReceived : SignatureStatus.Locked,
                CreatedAt = now.AddDays(-index), LockedAt = index >= 9 ? now.AddHours(-index) : null
            };
            if (index >= 6)
            {
                var signer = active[(index + 3) % active.Count];
                var offer = new SignatureOfferRecord
                {
                    Id = Guid.NewGuid(), Request = request, RequestId = request.Id, SignerId = signer.Id,
                    Status = index >= 9 ? SignatureOfferStatus.Selected : SignatureOfferStatus.Pending,
                    CreatedAt = now.AddHours(-index)
                };
                request.Offers.Add(offer);
                if (index >= 9) request.SignerId = signer.Id;
            }
            db.Signatures.Add(request);
            AddAudit(db, "Signature", request.Id, "DemoCreated", admin.Id, request.RequesterId,
                new { request.ServiceDate, request.Comment, request.Status, request.SignerId });
        }

        foreach (var agent in active.Take(8))
            db.Notifications.Add(new NotificationRecord
            {
                Id = Guid.NewGuid(), RecipientId = agent.Id, Type = NotificationType.SignatureOfferReceived,
                Message = "Un agent se propose pour signer à votre place.", EntityType = "Signature", EntityId = Guid.NewGuid(),
                CreatedAt = now.AddMinutes(-int.Parse(agent.Matricule[^1].ToString()))
            });

        await db.SaveChangesAsync();
        await PrintCountsAsync(db);
    }

    private static async Task<AgentUser> CreateUserAsync(UserManager<AgentUser> users, string matricule, string phone, AgentStatus status, AgentRole role)
    {
        var user = new AgentUser { Id = Guid.NewGuid(), UserName = matricule, Matricule = matricule, PhoneNumber = phone, Status = status, AppRole = role };
        var result = await users.CreateAsync(user, DemoPassword);
        if (!result.Succeeded) throw new InvalidOperationException($"Création de {matricule} impossible : {string.Join(" ", result.Errors.Select(x => x.Description))}");
        return user;
    }

    private static async Task ResetDemoPasswordsAsync(UserManager<AgentUser> users)
    {
        var demoUsers = await users.Users
            .Where(x => x.Matricule == "DELEGUE" || x.Matricule.StartsWith("70-"))
            .ToListAsync();

        foreach (var user in demoUsers)
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);

            var removeResult = await users.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
                throw new InvalidOperationException($"Suppression du mot de passe de {user.Matricule} impossible : {string.Join(" ", removeResult.Errors.Select(x => x.Description))}");

            var result = await users.AddPasswordAsync(user, DemoPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Réinitialisation de {user.Matricule} impossible : {string.Join(" ", result.Errors.Select(x => x.Description))}");
        }
    }

    private static void AddAudit(PermutStibDbContext db, string type, Guid entityId, string action, Guid actorId, Guid subjectId, object data) =>
        db.AuditLog.Add(new AuditRecord { EntityType = type, EntityId = entityId.ToString(), Action = action, ActorId = actorId,
            SubjectUserId = subjectId, AfterJson = JsonSerializer.Serialize(data) });

    private static async Task PrintCountsAsync(PermutStibDbContext db)
    {
        Console.WriteLine("DEMO_DATA_VERIFIED " + JsonSerializer.Serialize(new
        {
            Users = await db.Users.CountAsync(),
            Agents = await db.Users.CountAsync(x => x.AppRole == AgentRole.Agent),
            Admins = await db.Users.CountAsync(x => x.AppRole == AgentRole.Admin),
            Permutations = await db.Permutations.CountAsync(),
            Signatures = await db.Signatures.CountAsync(),
            Notifications = await db.Notifications.CountAsync(),
            AuditRecords = await db.AuditLog.CountAsync()
        }));
    }
}
