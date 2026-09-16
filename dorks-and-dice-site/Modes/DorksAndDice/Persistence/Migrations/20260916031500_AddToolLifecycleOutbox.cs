using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

[DbContext(typeof(DorksAndDiceDbContext))]
[Migration("20260916031500_AddToolLifecycleOutbox")]
public sealed class AddToolLifecycleOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dd_tool_lifecycle_outbox",
            columns: table => new
            {
                EventId = table.Column<Guid>(nullable: false),
                TargetToolSlug = table.Column<string>(maxLength: 80, nullable: false),
                EventType = table.Column<string>(maxLength: 80, nullable: false),
                SubjectId = table.Column<Guid>(nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(nullable: false),
                AttemptCount = table.Column<int>(nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(nullable: false),
                DeliveredAt = table.Column<DateTimeOffset>(nullable: true),
                LastError = table.Column<string>(maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_dd_tool_lifecycle_outbox", x => x.EventId));

        migrationBuilder.CreateIndex(
            name: "IX_dd_tool_lifecycle_outbox_DeliveredAt_NextAttemptAt",
            table: "dd_tool_lifecycle_outbox",
            columns: new[] { "DeliveredAt", "NextAttemptAt" });

        migrationBuilder.CreateIndex(
            name: "IX_dd_tool_lifecycle_outbox_TargetToolSlug",
            table: "dd_tool_lifecycle_outbox",
            column: "TargetToolSlug");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "dd_tool_lifecycle_outbox");
    }
}
