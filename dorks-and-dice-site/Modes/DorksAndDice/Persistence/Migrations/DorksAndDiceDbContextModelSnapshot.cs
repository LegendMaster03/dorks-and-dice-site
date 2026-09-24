using dorks_and_dice_site.Modes.DorksAndDice.Campaigns;
using dorks_and_dice_site.Modes.DorksAndDice.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

[DbContext(typeof(DorksAndDiceDbContext))]
public sealed class DorksAndDiceDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.10");

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.Campaign", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<DateTimeOffset?>("ArchivedAt");
            b.Property<Guid?>("ArchivedByUserId");
            b.Property<DateTimeOffset>("CreatedAt");
            b.Property<Guid>("CreatedByUserId");
            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(160);
            b.Property<CampaignStatus>("Status");
            b.Property<DateTimeOffset>("UpdatedAt");

            b.HasKey("Id");
            b.HasIndex("CreatedByUserId");
            b.HasIndex("Status");

            b.ToTable("dd_campaign");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignInvitation", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<DateTimeOffset?>("AcceptedAt");
            b.Property<Guid?>("AcceptedByUserId");
            b.Property<Guid>("CampaignId");
            b.Property<DateTimeOffset>("CreatedAt");
            b.Property<Guid>("CreatedByUserId");
            b.Property<DateTimeOffset>("ExpiresAt");
            b.Property<Guid?>("ParticipantId");
            b.Property<DateTimeOffset?>("RevokedAt");
            b.Property<Guid?>("RevokedByUserId");
            b.Property<string>("Roles")
                .IsRequired()
                .HasMaxLength(100);
            b.Property<CampaignInvitationStatus>("Status")
                .IsConcurrencyToken();
            b.Property<string>("TokenHash")
                .IsRequired()
                .HasMaxLength(64);

            b.HasKey("Id");

            b.HasIndex("CampaignId", "Status");

            b.HasIndex("ParticipantId")
                .IsUnique()
                .HasDatabaseName("IX_dd_campaign_invitation_pending_participant")
                .HasFilter("\"Status\" = 0 AND \"ParticipantId\" IS NOT NULL");

            b.HasIndex("TokenHash")
                .IsUnique();

            b.ToTable("dd_campaign_invitation");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignMembership", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<Guid>("CampaignId");
            b.Property<DateTimeOffset?>("EndedAt");
            b.Property<Guid?>("EndedByUserId");
            b.Property<string>("EndReason")
                .HasMaxLength(300);
            b.Property<DateTimeOffset>("JoinedAt");
            b.Property<CampaignMembershipStatus>("Status");
            b.Property<Guid>("UserId");

            b.HasKey("Id");

            b.HasIndex("CampaignId", "Status");

            b.HasIndex("CampaignId", "UserId")
                .IsUnique()
                .HasDatabaseName("IX_dd_campaign_membership_active_user")
                .HasFilter("\"Status\" = 0");

            b.ToTable("dd_campaign_membership");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignMembershipRole", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<Guid>("CampaignMembershipId");
            b.Property<DateTimeOffset>("GrantedAt");
            b.Property<Guid>("GrantedByUserId");
            b.Property<string>("Role")
                .IsRequired()
                .HasMaxLength(40);

            b.HasKey("Id");

            b.HasIndex("CampaignMembershipId", "Role")
                .IsUnique();

            b.ToTable("dd_campaign_membership_role");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignParticipant", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<Guid>("CampaignId");
            b.Property<DateTimeOffset>("CreatedAt");
            b.Property<DateTimeOffset?>("EndedAt");
            b.Property<Guid?>("EndedByUserId");
            b.Property<string>("EndReason")
                .HasMaxLength(300);
            b.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(120);
            b.Property<CampaignParticipantStatus>("Status");
            b.Property<Guid?>("UserId");

            b.HasKey("Id");

            b.HasIndex("CampaignId");

            b.HasIndex("CampaignId", "UserId")
                .IsUnique()
                .HasDatabaseName("IX_dd_campaign_participant_active_user")
                .HasFilter("\"Status\" = 0 AND \"UserId\" IS NOT NULL");

            b.HasIndex("UserId");

            b.ToTable("dd_campaign_participant");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Characters.CampaignCharacterAssociation", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<Guid>("CampaignId");
            b.Property<Guid>("CharacterId");
            b.Property<DateTimeOffset>("ConnectedAt");
            b.Property<DateTimeOffset?>("EndedAt");
            b.Property<Guid?>("EndedByUserId");
            b.Property<string>("EndReason")
                .HasMaxLength(300);
            b.Property<CampaignCharacterAssociationStatus>("Status");

            b.HasKey("Id");

            b.HasIndex("CampaignId", "CharacterId")
                .IsUnique()
                .HasDatabaseName("IX_dd_campaign_character_active_pair")
                .HasFilter("\"Status\" = 0");

            b.HasIndex("CampaignId", "Status");

            b.HasIndex("CharacterId", "Status");

            b.ToTable("dd_campaign_character");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Characters.Character", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<DateTimeOffset?>("ArchivedAt");
            b.Property<DateTimeOffset>("CreatedAt");
            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(160);
            b.Property<Guid>("OwnerUserId");
            b.Property<CharacterStatus>("Status");
            b.Property<DateTimeOffset>("UpdatedAt");

            b.HasKey("Id");

            b.HasIndex("OwnerUserId");
            b.HasIndex("Status");

            b.ToTable("dd_character");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Discord.DorksAndDiceDiscordServerBinding", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            b.Property<int>("CampaignScope");
            b.Property<DateTimeOffset>("CreatedAt");
            b.Property<string>("GuildId")
                .IsRequired()
                .HasMaxLength(32);
            b.Property<Guid>("OwnerUserId");
            b.Property<DateTimeOffset>("UpdatedAt");

            b.HasKey("Id");

            b.HasIndex("GuildId")
                .IsUnique();

            b.HasIndex("OwnerUserId");

            b.ToTable("dd_discord_server");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Discord.DorksAndDiceDiscordServerCampaign", b =>
        {
            b.Property<Guid>("BindingId");
            b.Property<Guid>("CampaignId");

            b.HasKey("BindingId", "CampaignId");

            b.HasIndex("CampaignId");

            b.ToTable("dd_discord_server_campaign");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Lifecycle.ToolLifecycleOutboxEvent", b =>
        {
            b.Property<Guid>("EventId");

            b.Property<int>("AttemptCount");
            b.Property<DateTimeOffset?>("DeliveredAt");
            b.Property<string>("EventType")
                .IsRequired()
                .HasMaxLength(80);
            b.Property<string>("LastError")
                .HasMaxLength(2000);
            b.Property<DateTimeOffset>("NextAttemptAt");
            b.Property<DateTimeOffset>("OccurredAt");
            b.Property<Guid>("SubjectId");
            b.Property<string>("TargetToolSlug")
                .IsRequired()
                .HasMaxLength(80);

            b.HasKey("EventId");

            b.HasIndex("DeliveredAt", "NextAttemptAt");
            b.HasIndex("TargetToolSlug");

            b.ToTable("dd_tool_lifecycle_outbox");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Discord.DorksAndDiceDiscordServerCampaign", b =>
        {
            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Discord.DorksAndDiceDiscordServerBinding", "Binding")
                .WithMany("Campaigns")
                .HasForeignKey("BindingId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.Campaign", "Campaign")
                .WithMany()
                .HasForeignKey("CampaignId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Binding");
            b.Navigation("Campaign");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignInvitation", b =>
        {
            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.Campaign", "Campaign")
                .WithMany()
                .HasForeignKey("CampaignId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignParticipant", "Participant")
                .WithMany()
                .HasForeignKey("ParticipantId")
                .OnDelete(DeleteBehavior.Restrict);

            b.Navigation("Campaign");
            b.Navigation("Participant");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignMembership", b =>
        {
            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.Campaign", "Campaign")
                .WithMany("Memberships")
                .HasForeignKey("CampaignId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Campaign");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignMembershipRole", b =>
        {
            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignMembership", "CampaignMembership")
                .WithMany("Roles")
                .HasForeignKey("CampaignMembershipId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("CampaignMembership");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignParticipant", b =>
        {
            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.Campaign", "Campaign")
                .WithMany("Participants")
                .HasForeignKey("CampaignId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Campaign");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Characters.CampaignCharacterAssociation", b =>
        {
            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.Campaign", "Campaign")
                .WithMany("CharacterAssociations")
                .HasForeignKey("CampaignId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.HasOne("dorks_and_dice_site.Modes.DorksAndDice.Characters.Character", "Character")
                .WithMany("CampaignAssociations")
                .HasForeignKey("CharacterId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.Navigation("Campaign");
            b.Navigation("Character");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Discord.DorksAndDiceDiscordServerBinding", b =>
        {
            b.Navigation("Campaigns");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.Campaign", b =>
        {
            b.Navigation("CharacterAssociations");
            b.Navigation("Memberships");
            b.Navigation("Participants");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Campaigns.CampaignMembership", b =>
        {
            b.Navigation("Roles");
        });

        modelBuilder.Entity("dorks_and_dice_site.Modes.DorksAndDice.Characters.Character", b =>
        {
            b.Navigation("CampaignAssociations");
        });
    }
}
