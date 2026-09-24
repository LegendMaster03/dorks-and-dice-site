using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dorks_and_dice_site.Migrations.Identity;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260924033000_AddDiscordManagedChannels")]
public sealed class AddDiscordManagedChannels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DiscordManagedChannels",
            columns: table => new
            {
                SourceId = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                GuildId = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                ChannelKey = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false),
                DiscordChannelId = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false),
                Name = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                ParentKey = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_DiscordManagedChannels",
                    x => new { x.SourceId, x.GuildId, x.ChannelKey });
            });

        migrationBuilder.CreateIndex(
            name: "IX_DiscordManagedChannels_GuildId_DiscordChannelId",
            table: "DiscordManagedChannels",
            columns: new[] { "GuildId", "DiscordChannelId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DiscordManagedChannels");
    }
}
