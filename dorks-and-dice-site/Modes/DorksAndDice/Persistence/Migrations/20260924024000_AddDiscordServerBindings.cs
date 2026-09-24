using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

[DbContext(typeof(DorksAndDiceDbContext))]
[Migration("20260924024000_AddDiscordServerBindings")]
public sealed class AddDiscordServerBindings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dd_discord_server",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                GuildId = table.Column<string>(maxLength: 32, nullable: false),
                OwnerUserId = table.Column<Guid>(nullable: false),
                CampaignScope = table.Column<int>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_discord_server", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "dd_discord_server_campaign",
            columns: table => new
            {
                BindingId = table.Column<Guid>(nullable: false),
                CampaignId = table.Column<Guid>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_dd_discord_server_campaign",
                    x => new { x.BindingId, x.CampaignId });
                table.ForeignKey(
                    name: "FK_dd_discord_server_campaign_dd_discord_server_BindingId",
                    column: x => x.BindingId,
                    principalTable: "dd_discord_server",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_dd_discord_server_campaign_dd_campaign_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "dd_campaign",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_dd_discord_server_GuildId",
            table: "dd_discord_server",
            column: "GuildId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_dd_discord_server_OwnerUserId",
            table: "dd_discord_server",
            column: "OwnerUserId");

        migrationBuilder.CreateIndex(
            name: "IX_dd_discord_server_campaign_CampaignId",
            table: "dd_discord_server_campaign",
            column: "CampaignId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "dd_discord_server_campaign");
        migrationBuilder.DropTable(name: "dd_discord_server");
    }
}
