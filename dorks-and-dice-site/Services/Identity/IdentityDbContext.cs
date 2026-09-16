using dorks_and_dice_site.Models.Identity;
using dorks_and_dice_site.Models.Operator;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace dorks_and_dice_site.Services.Identity;

public sealed class IdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<OperatorCredential> OperatorCredentials => Set<OperatorCredential>();
    public DbSet<OperatorAuditRecord> OperatorAuditRecords => Set<OperatorAuditRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(value => value.AccountKind)
                .HasConversion<int>()
                .HasDefaultValue(AccountKind.Human)
                .IsRequired();
            user.Property(value => value.DisplayName)
                .HasMaxLength(ApplicationUser.DisplayNameMaxLength)
                .IsRequired();

            user.Property(value => value.CreatedAt)
                .IsRequired();

            user.HasIndex(value => value.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique();
        });

        builder.Entity<OperatorCredential>(credential =>
        {
            credential.ToTable("OperatorCredentials");
            credential.HasKey(value => value.Id);
            credential.Property(value => value.Name)
                .HasMaxLength(OperatorCredential.NameMaxLength)
                .IsRequired();
            credential.Property(value => value.SecretHash)
                .HasMaxLength(OperatorCredential.SecretHashMaxLength)
                .IsRequired();
            credential.Property(value => value.SecretPrefix)
                .HasMaxLength(OperatorCredential.SecretPrefixMaxLength)
                .IsRequired();
            credential.HasIndex(value => value.UserId);
            credential.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(value => value.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OperatorAuditRecord>(audit =>
        {
            audit.ToTable("OperatorAuditRecords");
            audit.HasKey(value => value.Id);
            audit.Property(value => value.Client)
                .HasMaxLength(OperatorAuditRecord.ClientMaxLength)
                .IsRequired();
            audit.Property(value => value.Capability)
                .HasMaxLength(OperatorAuditRecord.CapabilityMaxLength)
                .IsRequired();
            audit.Property(value => value.Resource)
                .HasMaxLength(OperatorAuditRecord.ResourceMaxLength)
                .IsRequired();
            audit.Property(value => value.Outcome)
                .HasMaxLength(OperatorAuditRecord.OutcomeMaxLength)
                .IsRequired();
            audit.HasIndex(value => value.InvocationId).IsUnique();
            audit.HasIndex(value => value.UserId);
            audit.HasIndex(value => value.CredentialId);
            audit.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(value => value.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            audit.HasOne<OperatorCredential>()
                .WithMany()
                .HasForeignKey(value => value.CredentialId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
