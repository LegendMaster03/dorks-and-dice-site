using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

[DbContext(typeof(DorksAndDiceDbContext))]
[Migration("20260914223500_InitialDorksAndDice")]
public sealed class InitialDorksAndDice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dd_campaign",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 160, nullable: false),
                CreatedByUserId = table.Column<Guid>(nullable: false),
                Status = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
                ArchivedAt = table.Column<DateTimeOffset>(nullable: true),
                ArchivedByUserId = table.Column<Guid>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_dd_campaign", x => x.Id));

        migrationBuilder.CreateTable(
            name: "dd_character",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                OwnerUserId = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 160, nullable: false),
                Status = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
                ArchivedAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_dd_character", x => x.Id));

        migrationBuilder.CreateTable(
            name: "dd_campaign_membership",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CampaignId = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: false),
                Status = table.Column<int>(nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(nullable: false),
                EndedAt = table.Column<DateTimeOffset>(nullable: true),
                EndedByUserId = table.Column<Guid>(nullable: true),
                EndReason = table.Column<string>(maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_campaign_membership", x => x.Id);
                table.ForeignKey("FK_dd_campaign_membership_dd_campaign_CampaignId", x => x.CampaignId, "dd_campaign", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "dd_campaign_participant",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CampaignId = table.Column<Guid>(nullable: false),
                UserId = table.Column<Guid>(nullable: true),
                DisplayName = table.Column<string>(maxLength: 120, nullable: false),
                Status = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                EndedAt = table.Column<DateTimeOffset>(nullable: true),
                EndedByUserId = table.Column<Guid>(nullable: true),
                EndReason = table.Column<string>(maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_campaign_participant", x => x.Id);
                table.ForeignKey("FK_dd_campaign_participant_dd_campaign_CampaignId", x => x.CampaignId, "dd_campaign", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "dd_campaign_character",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CampaignId = table.Column<Guid>(nullable: false),
                CharacterId = table.Column<Guid>(nullable: false),
                Status = table.Column<int>(nullable: false),
                ConnectedAt = table.Column<DateTimeOffset>(nullable: false),
                EndedAt = table.Column<DateTimeOffset>(nullable: true),
                EndedByUserId = table.Column<Guid>(nullable: true),
                EndReason = table.Column<string>(maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_campaign_character", x => x.Id);
                table.ForeignKey("FK_dd_campaign_character_dd_campaign_CampaignId", x => x.CampaignId, "dd_campaign", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_dd_campaign_character_dd_character_CharacterId", x => x.CharacterId, "dd_character", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "dd_campaign_membership_role",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CampaignMembershipId = table.Column<Guid>(nullable: false),
                Role = table.Column<string>(maxLength: 40, nullable: false),
                GrantedAt = table.Column<DateTimeOffset>(nullable: false),
                GrantedByUserId = table.Column<Guid>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_campaign_membership_role", x => x.Id);
                table.ForeignKey("FK_dd_campaign_membership_role_dd_campaign_membership_CampaignMembershipId", x => x.CampaignMembershipId, "dd_campaign_membership", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "dd_campaign_invitation",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CampaignId = table.Column<Guid>(nullable: false),
                ParticipantId = table.Column<Guid>(nullable: true),
                TokenHash = table.Column<string>(maxLength: 64, nullable: false),
                Roles = table.Column<string>(maxLength: 100, nullable: false),
                Status = table.Column<int>(nullable: false),
                CreatedByUserId = table.Column<Guid>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
                AcceptedAt = table.Column<DateTimeOffset>(nullable: true),
                AcceptedByUserId = table.Column<Guid>(nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(nullable: true),
                RevokedByUserId = table.Column<Guid>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_campaign_invitation", x => x.Id);
                table.ForeignKey("FK_dd_campaign_invitation_dd_campaign_CampaignId", x => x.CampaignId, "dd_campaign", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_dd_campaign_invitation_dd_campaign_participant_ParticipantId", x => x.ParticipantId, "dd_campaign_participant", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_dd_campaign_CreatedByUserId", "dd_campaign", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_dd_campaign_Status", "dd_campaign", "Status");
        migrationBuilder.CreateIndex("IX_dd_character_OwnerUserId", "dd_character", "OwnerUserId");
        migrationBuilder.CreateIndex("IX_dd_character_Status", "dd_character", "Status");
        migrationBuilder.CreateIndex("IX_dd_campaign_membership_CampaignId_UserId", "dd_campaign_membership", new[] { "CampaignId", "UserId" });
        migrationBuilder.CreateIndex("IX_dd_campaign_membership_CampaignId_Status", "dd_campaign_membership", new[] { "CampaignId", "Status" });
        migrationBuilder.CreateIndex("IX_dd_campaign_membership_active_user", "dd_campaign_membership", new[] { "CampaignId", "UserId" }, unique: true, filter: "\"Status\" = 0");
        migrationBuilder.CreateIndex("IX_dd_campaign_membership_role_CampaignMembershipId_Role", "dd_campaign_membership_role", new[] { "CampaignMembershipId", "Role" }, unique: true);
        migrationBuilder.CreateIndex("IX_dd_campaign_participant_CampaignId", "dd_campaign_participant", "CampaignId");
        migrationBuilder.CreateIndex("IX_dd_campaign_participant_UserId", "dd_campaign_participant", "UserId");
        migrationBuilder.CreateIndex("IX_dd_campaign_participant_active_user", "dd_campaign_participant", new[] { "CampaignId", "UserId" }, unique: true, filter: "\"Status\" = 0 AND \"UserId\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_dd_campaign_character_CampaignId_Status", "dd_campaign_character", new[] { "CampaignId", "Status" });
        migrationBuilder.CreateIndex("IX_dd_campaign_character_CharacterId_Status", "dd_campaign_character", new[] { "CharacterId", "Status" });
        migrationBuilder.CreateIndex("IX_dd_campaign_character_active_pair", "dd_campaign_character", new[] { "CampaignId", "CharacterId" }, unique: true, filter: "\"Status\" = 0");
        migrationBuilder.CreateIndex("IX_dd_campaign_character_CharacterId", "dd_campaign_character", "CharacterId");
        migrationBuilder.CreateIndex("IX_dd_campaign_invitation_CampaignId_Status", "dd_campaign_invitation", new[] { "CampaignId", "Status" });
        migrationBuilder.CreateIndex("IX_dd_campaign_invitation_TokenHash", "dd_campaign_invitation", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_dd_campaign_invitation_pending_participant", "dd_campaign_invitation", "ParticipantId", unique: true, filter: "\"Status\" = 0 AND \"ParticipantId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("dd_campaign_character");
        migrationBuilder.DropTable("dd_campaign_invitation");
        migrationBuilder.DropTable("dd_campaign_membership_role");
        migrationBuilder.DropTable("dd_character");
        migrationBuilder.DropTable("dd_campaign_participant");
        migrationBuilder.DropTable("dd_campaign_membership");
        migrationBuilder.DropTable("dd_campaign");
    }
}
