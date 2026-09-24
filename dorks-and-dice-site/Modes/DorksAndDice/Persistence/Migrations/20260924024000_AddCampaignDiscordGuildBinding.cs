using dorks_and_dice_site.Modes.DorksAndDice.Discord;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

[DbContext(typeof(DorksAndDiceDbContext))]
[Migration("20260924024000_AddCampaignDiscordGuildBinding")]
public sealed class AddCampaignDiscordGuildBinding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dd_campaign_discord_guild",
            columns: table => new
            {
                CampaignId = table.Column<Guid>(nullable: false),
                GuildId = table.Column<string>(
                    maxLength: CampaignDiscordGuildBinding.GuildIdMaxLength,
                    nullable: false),
                ConfiguredByUserId = table.Column<Guid>(nullable: false),
                ConfiguredAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_campaign_discord_guild", x => x.CampaignId);
                table.ForeignKey(
                    name: "FK_dd_campaign_discord_guild_dd_campaign_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "dd_campaign",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_dd_campaign_discord_guild_GuildId",
            table: "dd_campaign_discord_guild",
            column: "GuildId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "dd_campaign_discord_guild");
    }
}
