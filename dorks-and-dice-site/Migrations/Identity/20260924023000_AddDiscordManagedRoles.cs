using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dorks_and_dice_site.Migrations.Identity;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260924023000_AddDiscordManagedRoles")]
public sealed class AddDiscordManagedRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DiscordManagedRoles",
            columns: table => new
            {
                SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                GuildId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                RoleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                DiscordRoleId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiscordManagedRoles", x => new { x.SourceId, x.GuildId, x.RoleKey });
            });

        migrationBuilder.CreateTable(
            name: "DiscordManagedRoleAssignments",
            columns: table => new
            {
                SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                GuildId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                RoleKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                DiscordUserId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DiscordRoleId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_DiscordManagedRoleAssignments",
                    x => new { x.SourceId, x.GuildId, x.RoleKey, x.DiscordUserId });
            });

        migrationBuilder.CreateIndex(
            name: "IX_DiscordManagedRoles_GuildId_DiscordRoleId",
            table: "DiscordManagedRoles",
            columns: new[] { "GuildId", "DiscordRoleId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DiscordManagedRoleAssignments_GuildId_DiscordUserId",
            table: "DiscordManagedRoleAssignments",
            columns: new[] { "GuildId", "DiscordUserId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DiscordManagedRoleAssignments");
        migrationBuilder.DropTable(name: "DiscordManagedRoles");
    }
}
