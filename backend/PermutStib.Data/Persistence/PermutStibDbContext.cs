using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PermutStib.Data.Entities;

namespace PermutStib.Data.Persistence;

public sealed class PermutStibDbContext(DbContextOptions<PermutStibDbContext> options)
    : IdentityDbContext<AgentUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PermutationRecord> Permutations => Set<PermutationRecord>();
    public DbSet<SignatureRecord> Signatures => Set<SignatureRecord>();
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
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.HasIndex(x => new { x.RequesterId, x.Status });
        });

        builder.Entity<SignatureRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.HasIndex(x => new { x.ServiceDate, x.Status });
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
}

