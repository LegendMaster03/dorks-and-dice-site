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

    public DbSet<AccountLinkModeActivation> AccountLinkModeActivations => Set<AccountLinkModeActivation>();
    public DbSet<DiscordManagedRole> DiscordManagedRoles => Set<DiscordManagedRole>();
    public DbSet<DiscordManagedRoleAssignment> DiscordManagedRoleAssignments => Set<DiscordManagedRoleAssignment>();
    public DbSet<OperatorCredential> OperatorCredentials => Set<OperatorCredential>();
    public DbSet<OperatorBrowserBootstrap> OperatorBrowserBootstraps => Set<OperatorBrowserBootstrap>();
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

        builder.Entity<AccountLinkModeActivation>(activation =>
        {
            activation.ToTable("AccountLinkModeActivations");
            activation.HasKey(value => new { value.UserId, value.ModeId, value.ProviderId });
            activation.Property(value => value.ModeId)
                .HasMaxLength(AccountLinkModeActivation.ModeIdMaxLength)
                .IsRequired();
            activation.Property(value => value.ProviderId)
                .HasMaxLength(AccountLinkModeActivation.ProviderIdMaxLength)
                .IsRequired();
            activation.Property(value => value.ResourceId)
                .HasMaxLength(AccountLinkModeActivation.ResourceIdMaxLength)
                .IsRequired();
            activation.Property(value => value.ActivatedAt)
                .IsRequired();
            activation.HasIndex(value => new { value.ModeId, value.ProviderId });
            activation.HasIndex(value => new { value.UserId, value.ProviderId });
            activation.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(value => value.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DiscordManagedRole>(role =>
        {
            role.ToTable("DiscordManagedRoles");
            role.HasKey(value => new { value.SourceId, value.GuildId, value.RoleKey });
            role.Property(value => value.SourceId)
                .HasMaxLength(DiscordManagedRole.SourceIdMaxLength)
                .IsRequired();
            role.Property(value => value.GuildId)
                .HasMaxLength(DiscordManagedRole.GuildIdMaxLength)
                .IsRequired();
            role.Property(value => value.RoleKey)
                .HasMaxLength(DiscordManagedRole.RoleKeyMaxLength)
                .IsRequired();
            role.Property(value => value.DiscordRoleId)
                .HasMaxLength(DiscordManagedRole.DiscordRoleIdMaxLength)
                .IsRequired();
            role.Property(value => value.DisplayName)
                .HasMaxLength(DiscordManagedRole.DisplayNameMaxLength)
                .IsRequired();
            role.Property(value => value.UpdatedAt)
                .IsRequired();
            role.HasIndex(value => new { value.GuildId, value.DiscordRoleId }).IsUnique();
        });

        builder.Entity<DiscordManagedRoleAssignment>(assignment =>
        {
            assignment.ToTable("DiscordManagedRoleAssignments");
            assignment.HasKey(value => new
            {
                value.SourceId,
                value.GuildId,
                value.RoleKey,
                value.DiscordUserId
            });
            assignment.Property(value => value.SourceId)
                .HasMaxLength(DiscordManagedRole.SourceIdMaxLength)
                .IsRequired();
            assignment.Property(value => value.GuildId)
                .HasMaxLength(DiscordManagedRole.GuildIdMaxLength)
                .IsRequired();
            assignment.Property(value => value.RoleKey)
                .HasMaxLength(DiscordManagedRole.RoleKeyMaxLength)
                .IsRequired();
            assignment.Property(value => value.DiscordUserId)
                .HasMaxLength(DiscordManagedRoleAssignment.DiscordUserIdMaxLength)
                .IsRequired();
            assignment.Property(value => value.DiscordRoleId)
                .HasMaxLength(DiscordManagedRole.DiscordRoleIdMaxLength)
                .IsRequired();
            assignment.Property(value => value.UpdatedAt)
                .IsRequired();
            assignment.HasIndex(value => new
            {
                value.GuildId,
                value.DiscordUserId
            });
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

        builder.Entity<OperatorBrowserBootstrap>(bootstrap =>
        {
            bootstrap.ToTable("OperatorBrowserBootstraps");
            bootstrap.HasKey(value => value.Id);
            bootstrap.Property(value => value.SecretHash)
                .HasMaxLength(OperatorBrowserBootstrap.SecretHashMaxLength)
                .IsRequired();
            bootstrap.HasIndex(value => value.UserId);
            bootstrap.HasIndex(value => value.CredentialId);
            bootstrap.HasIndex(value => value.ExpiresAt);
            bootstrap.HasIndex(value => value.IssuanceInvocationId).IsUnique();
            bootstrap.HasOne<OperatorCredential>()
                .WithMany()
                .HasForeignKey(value => value.CredentialId)
                .OnDelete(DeleteBehavior.Restrict);
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
