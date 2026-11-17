using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

/// <summary>
/// Durable Site-side admission/revocation records. This migration does not
/// activate the R3 protocol; existing authorization routes remain unchanged.
/// </summary>
[DbContext(typeof(DorksAndDiceDbContext))]
[Migration("20261117030000_AddProtectedMutationCoordinator")]
public sealed class AddProtectedMutationCoordinator : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "dd_protected_mutation_scope",
            columns: table => new
            {
                ScopeKey = table.Column<string>(maxLength: 180, nullable: false),
                Generation = table.Column<long>(nullable: false),
                PendingRevocationId = table.Column<Guid>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_dd_protected_mutation_scope", x => x.ScopeKey));
        migrationBuilder.CreateIndex(
            name: "IX_dd_protected_mutation_scope_PendingRevocationId",
            table: "dd_protected_mutation_scope", column: "PendingRevocationId");

        migrationBuilder.CreateTable(
            name: "dd_protected_mutation_intent",
            columns: table => new
            {
                OperationId = table.Column<Guid>(nullable: false),
                ActorUserId = table.Column<Guid>(nullable: false),
                CharacterId = table.Column<Guid>(nullable: false),
                CampaignId = table.Column<Guid>(nullable: true),
                OperationKind = table.Column<string>(maxLength: 80, nullable: false),
                CommandDigest = table.Column<string>(maxLength: 64, nullable: false),
                Status = table.Column<int>(nullable: false),
                AdmittedAt = table.Column<DateTimeOffset>(nullable: false),
                TerminalObservedAt = table.Column<DateTimeOffset>(nullable: true),
                TerminalReceiptDigest = table.Column<string>(maxLength: 64, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_dd_protected_mutation_intent", x => x.OperationId));
        migrationBuilder.CreateIndex(
            name: "IX_dd_protected_mutation_intent_CharacterId_Status",
            table: "dd_protected_mutation_intent", columns: new[] { "CharacterId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_dd_protected_mutation_intent_ActorUserId_Status",
            table: "dd_protected_mutation_intent", columns: new[] { "ActorUserId", "Status" });

        migrationBuilder.CreateTable(
            name: "dd_protected_mutation_revocation",
            columns: table => new
            {
                RevocationId = table.Column<Guid>(nullable: false),
                InitiatorUserId = table.Column<Guid>(nullable: false),
                ChangeKind = table.Column<string>(maxLength: 80, nullable: false),
                SubjectId = table.Column<Guid>(nullable: false),
                Status = table.Column<int>(nullable: false),
                RequestedAt = table.Column<DateTimeOffset>(nullable: false),
                EffectiveAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_dd_protected_mutation_revocation", x => x.RevocationId));
        migrationBuilder.CreateIndex(
            name: "IX_dd_protected_mutation_revocation_SubjectId_Status",
            table: "dd_protected_mutation_revocation", columns: new[] { "SubjectId", "Status" });

        migrationBuilder.CreateTable(
            name: "dd_protected_mutation_intent_scope",
            columns: table => new
            {
                OperationId = table.Column<Guid>(nullable: false),
                ScopeKey = table.Column<string>(maxLength: 180, nullable: false),
                AdmittedGeneration = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_protected_mutation_intent_scope", x => new { x.OperationId, x.ScopeKey });
                table.ForeignKey("FK_dd_r3_intscope_intent",
                    x => x.OperationId, "dd_protected_mutation_intent", "OperationId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_dd_r3_intscope_gate",
                    x => x.ScopeKey, "dd_protected_mutation_scope", "ScopeKey", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(
            name: "IX_dd_protected_mutation_intent_scope_ScopeKey",
            table: "dd_protected_mutation_intent_scope", column: "ScopeKey");

        migrationBuilder.CreateTable(
            name: "dd_protected_mutation_revocation_scope",
            columns: table => new
            {
                RevocationId = table.Column<Guid>(nullable: false),
                ScopeKey = table.Column<string>(maxLength: 180, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dd_protected_mutation_revocation_scope", x => new { x.RevocationId, x.ScopeKey });
                table.ForeignKey("FK_dd_r3_revscope_revocation",
                    x => x.RevocationId, "dd_protected_mutation_revocation", "RevocationId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_dd_r3_revscope_gate",
                    x => x.ScopeKey, "dd_protected_mutation_scope", "ScopeKey", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(
            name: "IX_dd_protected_mutation_revocation_scope_ScopeKey",
            table: "dd_protected_mutation_revocation_scope", column: "ScopeKey");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "dd_protected_mutation_revocation_scope");
        migrationBuilder.DropTable(name: "dd_protected_mutation_intent_scope");
        migrationBuilder.DropTable(name: "dd_protected_mutation_revocation");
        migrationBuilder.DropTable(name: "dd_protected_mutation_intent");
        migrationBuilder.DropTable(name: "dd_protected_mutation_scope");
    }
}
