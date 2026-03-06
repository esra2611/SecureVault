using Microsoft.EntityFrameworkCore;

namespace SecureVault.Infrastructure.Persistence;

public sealed class SecretVaultDbContext : DbContext
{
    public SecretVaultDbContext(DbContextOptions<SecretVaultDbContext> options) : base(options) { }

    public DbSet<SecretEntity> Secrets => Set<SecretEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLogEntity>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.MessageId).HasMaxLength(64);
            e.HasIndex(x => x.MessageId).IsUnique().HasFilter("\"MessageId\" IS NOT NULL");
            e.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            e.Property(x => x.PayloadJson).IsRequired();
            e.Property(x => x.OccurredAtUtc).IsRequired();
            e.Property(x => x.CreatedAtUtc).IsRequired();
        });
        modelBuilder.Entity<SecretEntity>(e =>
        {
            e.ToTable("Secrets");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHashBase64).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.TokenHashBase64).IsUnique();
            e.Property(x => x.ExpiryType).IsRequired();
            e.Property(x => x.UtcCreatedAt).IsRequired();
            e.Property(x => x.UtcExpiresAt).IsRequired();
            e.Property(x => x.UtcRevealedAt);
            e.Property(x => x.Ciphertext);
            e.Property(x => x.Nonce).HasMaxLength(12);
            e.Property(x => x.SaltForPassword).HasMaxLength(16);
            e.Property(x => x.KeyVersion).IsRequired();
            e.Property(x => x.IsPasswordProtected).IsRequired();
            e.Property(x => x.PasswordHashBase64).HasMaxLength(256);
        });
    }
}
