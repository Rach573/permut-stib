using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PermutStib.Data.Entities;

namespace PermutStib.Data.Persistence;

public sealed class PermutStibDbContext(DbContextOptions<PermutStibDbContext> options)
    : IdentityDbContext<AgentUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PermutationRecord> Permutations => Set<PermutationRecord>();
    public DbSet<PermutationProposalRecord> PermutationProposals => Set<PermutationProposalRecord>();
    public DbSet<SignatureRecord> Signatures => Set<SignatureRecord>();
    public DbSet<SignatureOfferRecord> SignatureOffers => Set<SignatureOfferRecord>();
    public DbSet<AuditRecord> AuditLog => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AgentUser>(entity =>
        {
            entity.HasIndex(x => x.Matricule).IsUnique();
            entity.Property(x => x.Matricule).HasMaxLength(20);
            entity.Property(x => x.PhoneNumber).HasMaxLength(32);
        });

        builder.Entity<PermutationRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.RequesterId, x.Status });
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasMany(x => x.Proposals).WithOne(x => x.Request).HasForeignKey(x => x.RequestId);
        });

        builder.Entity<PermutationProposalRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.RequestId, x.PartnerId }).IsUnique();
        });

        builder.Entity<SignatureRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Comment).HasMaxLength(500);
            entity.HasIndex(x => new { x.ServiceDate, x.Status });
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasMany(x => x.Offers).WithOne(x => x.Request).HasForeignKey(x => x.RequestId);
        });

        builder.Entity<SignatureOfferRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.RequestId, x.SignerId }).IsUnique();
        });

        builder.Entity<AuditRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityType).HasMaxLength(64);
            entity.Property(x => x.EntityId).HasMaxLength(64);
            entity.Property(x => x.Action).HasMaxLength(64);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectAuditLog();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectAuditLog();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectAuditLog()
    {
        if (ChangeTracker.Entries<AuditRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Le journal d'audit est en ajout uniquement.");
    }
}
