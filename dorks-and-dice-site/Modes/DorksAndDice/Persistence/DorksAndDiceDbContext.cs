using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using dorks_and_dice_site.Modes.DorksAndDice.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence;

public sealed class DorksAndDiceDbContext(DbContextOptions<DorksAndDiceDbContext> options) : DbContext(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMembership> CampaignMemberships => Set<CampaignMembership>();
    public DbSet<CampaignMembershipRole> CampaignMembershipRoles => Set<CampaignMembershipRole>();
    public DbSet<CampaignParticipant> CampaignParticipants => Set<CampaignParticipant>();
    public DbSet<CampaignInvitation> CampaignInvitations => Set<CampaignInvitation>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CampaignCharacterAssociation> CampaignCharacters => Set<CampaignCharacterAssociation>();
    public DbSet<DorksAndDiceDiscordServerBinding> DiscordServerBindings => Set<DorksAndDiceDiscordServerBinding>();
    public DbSet<DorksAndDiceDiscordServerCampaign> DiscordServerCampaigns => Set<DorksAndDiceDiscordServerCampaign>();
    public DbSet<ToolLifecycleOutboxEvent> ToolLifecycleOutboxEvents => Set<ToolLifecycleOutboxEvent>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("dd_campaign");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(item => item.CreatedByUserId);
            entity.HasIndex(item => item.Status);
        });
        modelBuilder.Entity<CampaignMembership>(entity =>
        {
            entity.ToTable("dd_campaign_membership");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EndReason).HasMaxLength(300);
            entity.HasIndex(item => new { item.CampaignId, item.UserId });
            entity.HasIndex(item => new { item.CampaignId, item.UserId }).HasDatabaseName("IX_dd_campaign_membership_active_user").HasFilter("\"Status\" = 0").IsUnique();
            entity.HasIndex(item => new { item.CampaignId, item.Status });
            entity.HasOne(item => item.Campaign).WithMany(item => item.Memberships).HasForeignKey(item => item.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CampaignMembershipRole>(entity =>
        {
            entity.ToTable("dd_campaign_membership_role");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Role).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => new { item.CampaignMembershipId, item.Role }).IsUnique();
            entity.HasOne(item => item.CampaignMembership).WithMany(item => item.Roles).HasForeignKey(item => item.CampaignMembershipId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CampaignParticipant>(entity =>
        {
            entity.ToTable("dd_campaign_participant");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.EndReason).HasMaxLength(300);
            entity.HasIndex(item => item.CampaignId);
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => new { item.CampaignId, item.UserId }).HasDatabaseName("IX_dd_campaign_participant_active_user").HasFilter("\"Status\" = 0 AND \"UserId\" IS NOT NULL").IsUnique();
            entity.HasOne(item => item.Campaign).WithMany(item => item.Participants).HasForeignKey(item => item.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CampaignInvitation>(entity =>
        {
            entity.ToTable("dd_campaign_invitation");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Roles).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Status).IsConcurrencyToken();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => new { item.CampaignId, item.Status });
            entity.HasIndex(item => item.ParticipantId).HasDatabaseName("IX_dd_campaign_invitation_pending_participant").HasFilter("\"Status\" = 0 AND \"ParticipantId\" IS NOT NULL").IsUnique();
            entity.HasOne(item => item.Campaign).WithMany().HasForeignKey(item => item.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Participant).WithMany().HasForeignKey(item => item.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Character>(entity =>
        {
            entity.ToTable("dd_character");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(item => item.OwnerUserId);
            entity.HasIndex(item => item.Status);
        });
        modelBuilder.Entity<CampaignCharacterAssociation>(entity =>
        {
            entity.ToTable("dd_campaign_character");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EndReason).HasMaxLength(300);
            entity.HasIndex(item => new { item.CampaignId, item.Status });
            entity.HasIndex(item => new { item.CharacterId, item.Status });
            entity.HasIndex(item => new { item.CampaignId, item.CharacterId }).HasDatabaseName("IX_dd_campaign_character_active_pair").HasFilter("\"Status\" = 0").IsUnique();
            entity.HasOne(item => item.Campaign).WithMany(item => item.CharacterAssociations).HasForeignKey(item => item.CampaignId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Character).WithMany(item => item.CampaignAssociations).HasForeignKey(item => item.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DorksAndDiceDiscordServerBinding>(entity =>
        {
            entity.ToTable("dd_discord_server");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.GuildId)
                .HasMaxLength(DorksAndDiceDiscordServerBinding.GuildIdMaxLength)
                .IsRequired();
            entity.Property(item => item.GuildName)
                .HasMaxLength(DorksAndDiceDiscordServerBinding.GuildNameMaxLength)
                .IsRequired();
            entity.Property(item => item.CampaignScope)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(item => item.CreatedAt).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
            entity.HasIndex(item => item.GuildId).IsUnique();
            entity.HasIndex(item => item.OwnerUserId);
        });

        modelBuilder.Entity<DorksAndDiceDiscordServerCampaign>(entity =>
        {
            entity.ToTable("dd_discord_server_campaign");
            entity.HasKey(item => new { item.BindingId, item.CampaignId });
            entity.HasIndex(item => item.CampaignId);
            entity.HasOne(item => item.Binding)
                .WithMany(item => item.Campaigns)
                .HasForeignKey(item => item.BindingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Campaign)
                .WithMany()
                .HasForeignKey(item => item.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ToolLifecycleOutboxEvent>(entity =>
        {
            entity.ToTable("dd_tool_lifecycle_outbox");
            entity.HasKey(item => item.EventId);
            entity.Property(item => item.EventId).ValueGeneratedNever();
            entity.Property(item => item.TargetToolSlug).HasMaxLength(80).IsRequired();
            entity.Property(item => item.EventType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.SubjectId).IsRequired();
            entity.Property(item => item.OccurredAt).IsRequired();
            entity.Property(item => item.AttemptCount).IsRequired();
            entity.Property(item => item.NextAttemptAt).IsRequired();
            entity.Property(item => item.LastError).HasMaxLength(2000);
            entity.HasIndex(item => new { item.DeliveredAt, item.NextAttemptAt });
            entity.HasIndex(item => item.TargetToolSlug);
        });
    }
}
