using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dorks_and_dice_site.Migrations.Identity;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260924013000_AddAccountLinkModeActivations")]
public sealed class AddAccountLinkModeActivations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccountLinkModeActivations",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ModeId = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                ProviderId = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false),
                ResourceId = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false),
                ActivatedAt = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_AccountLinkModeActivations",
                    x => new { x.UserId, x.ModeId, x.ProviderId });
                table.ForeignKey(
                    name: "FK_AccountLinkModeActivations_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccountLinkModeActivations_ModeId_ProviderId",
            table: "AccountLinkModeActivations",
            columns: new[] { "ModeId", "ProviderId" });

        migrationBuilder.CreateIndex(
            name: "IX_AccountLinkModeActivations_UserId_ProviderId",
            table: "AccountLinkModeActivations",
            columns: new[] { "UserId", "ProviderId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AccountLinkModeActivations");
    }
}
