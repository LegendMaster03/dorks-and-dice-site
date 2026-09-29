using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

[DbContext(typeof(DorksAndDiceDbContext))]
[Migration("20260929014000_RemoveCampaignParticipants")]
public sealed class RemoveCampaignParticipants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_dd_campaign_invitation_dd_campaign_participant_ParticipantId",
            table: "dd_campaign_invitation");
        migrationBuilder.DropIndex(
            name: "IX_dd_campaign_invitation_pending_participant",
            table: "dd_campaign_invitation");
        migrationBuilder.DropColumn(
            name: "ParticipantId",
            table: "dd_campaign_invitation");
        migrationBuilder.DropTable(name: "dd_campaign_participant");
        migrationBuilder.AddColumn<bool>(
            name: "IsReusable",
            table: "dd_campaign_invitation",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsReusable", table: "dd_campaign_invitation");
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
                table.ForeignKey(
                    name: "FK_dd_campaign_participant_dd_campaign_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "dd_campaign",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.AddColumn<Guid>(
            name: "ParticipantId",
            table: "dd_campaign_invitation",
            nullable: true);
        migrationBuilder.CreateIndex(name: "IX_dd_campaign_participant_CampaignId", table: "dd_campaign_participant", column: "CampaignId");
        migrationBuilder.CreateIndex(name: "IX_dd_campaign_participant_UserId", table: "dd_campaign_participant", column: "UserId");
        migrationBuilder.CreateIndex(
            name: "IX_dd_campaign_participant_active_user",
            table: "dd_campaign_participant",
            columns: new[] { "CampaignId", "UserId" },
            unique: true,
            filter: "\"Status\" = 0 AND \"UserId\" IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_dd_campaign_invitation_pending_participant",
            table: "dd_campaign_invitation",
            column: "ParticipantId",
            unique: true,
            filter: "\"Status\" = 0 AND \"ParticipantId\" IS NOT NULL");
        migrationBuilder.AddForeignKey(
            name: "FK_dd_campaign_invitation_dd_campaign_participant_ParticipantId",
            table: "dd_campaign_invitation",
            column: "ParticipantId",
            principalTable: "dd_campaign_participant",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
