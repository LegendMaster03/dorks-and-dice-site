using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence.Migrations;

[DbContext(typeof(DorksAndDiceDbContext))]
[Migration("20260929014000_RemoveCampaignParticipants")]
public sealed class RemoveCampaignParticipants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "dd_campaign_invitation_new" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_dd_campaign_invitation_new" PRIMARY KEY,
                    "CampaignId" TEXT NOT NULL,
                    "TokenHash" TEXT NOT NULL,
                    "Roles" TEXT NOT NULL,
                    "IsReusable" INTEGER NOT NULL DEFAULT 0,
                    "Status" INTEGER NOT NULL,
                    "CreatedByUserId" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "AcceptedAt" TEXT NULL,
                    "AcceptedByUserId" TEXT NULL,
                    "RevokedAt" TEXT NULL,
                    "RevokedByUserId" TEXT NULL,
                    CONSTRAINT "FK_dd_campaign_invitation_dd_campaign_CampaignId"
                        FOREIGN KEY ("CampaignId") REFERENCES "dd_campaign" ("Id") ON DELETE CASCADE
                );

                INSERT INTO "dd_campaign_invitation_new" (
                    "Id", "CampaignId", "TokenHash", "Roles", "IsReusable", "Status",
                    "CreatedByUserId", "CreatedAt", "ExpiresAt", "AcceptedAt",
                    "AcceptedByUserId", "RevokedAt", "RevokedByUserId")
                SELECT
                    "Id", "CampaignId", "TokenHash", "Roles", 0, "Status",
                    "CreatedByUserId", "CreatedAt", "ExpiresAt", "AcceptedAt",
                    "AcceptedByUserId", "RevokedAt", "RevokedByUserId"
                FROM "dd_campaign_invitation";

                DROP TABLE "dd_campaign_invitation";
                ALTER TABLE "dd_campaign_invitation_new" RENAME TO "dd_campaign_invitation";

                CREATE INDEX "IX_dd_campaign_invitation_CampaignId_Status"
                    ON "dd_campaign_invitation" ("CampaignId", "Status");
                CREATE UNIQUE INDEX "IX_dd_campaign_invitation_TokenHash"
                    ON "dd_campaign_invitation" ("TokenHash");

                DROP TABLE "dd_campaign_participant";
                """);
            return;
        }

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
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "dd_campaign_participant" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_dd_campaign_participant" PRIMARY KEY,
                    "CampaignId" TEXT NOT NULL,
                    "UserId" TEXT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "EndedAt" TEXT NULL,
                    "EndedByUserId" TEXT NULL,
                    "EndReason" TEXT NULL,
                    CONSTRAINT "FK_dd_campaign_participant_dd_campaign_CampaignId"
                        FOREIGN KEY ("CampaignId") REFERENCES "dd_campaign" ("Id") ON DELETE CASCADE
                );

                CREATE INDEX "IX_dd_campaign_participant_CampaignId"
                    ON "dd_campaign_participant" ("CampaignId");
                CREATE INDEX "IX_dd_campaign_participant_UserId"
                    ON "dd_campaign_participant" ("UserId");
                CREATE UNIQUE INDEX "IX_dd_campaign_participant_active_user"
                    ON "dd_campaign_participant" ("CampaignId", "UserId")
                    WHERE "Status" = 0 AND "UserId" IS NOT NULL;

                CREATE TABLE "dd_campaign_invitation_old" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_dd_campaign_invitation_old" PRIMARY KEY,
                    "CampaignId" TEXT NOT NULL,
                    "ParticipantId" TEXT NULL,
                    "TokenHash" TEXT NOT NULL,
                    "Roles" TEXT NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "CreatedByUserId" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "AcceptedAt" TEXT NULL,
                    "AcceptedByUserId" TEXT NULL,
                    "RevokedAt" TEXT NULL,
                    "RevokedByUserId" TEXT NULL,
                    CONSTRAINT "FK_dd_campaign_invitation_dd_campaign_CampaignId"
                        FOREIGN KEY ("CampaignId") REFERENCES "dd_campaign" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_dd_campaign_invitation_dd_campaign_participant_ParticipantId"
                        FOREIGN KEY ("ParticipantId") REFERENCES "dd_campaign_participant" ("Id") ON DELETE RESTRICT
                );

                INSERT INTO "dd_campaign_invitation_old" (
                    "Id", "CampaignId", "ParticipantId", "TokenHash", "Roles", "Status",
                    "CreatedByUserId", "CreatedAt", "ExpiresAt", "AcceptedAt",
                    "AcceptedByUserId", "RevokedAt", "RevokedByUserId")
                SELECT
                    "Id", "CampaignId", NULL, "TokenHash", "Roles", "Status",
                    "CreatedByUserId", "CreatedAt", "ExpiresAt", "AcceptedAt",
                    "AcceptedByUserId", "RevokedAt", "RevokedByUserId"
                FROM "dd_campaign_invitation";

                DROP TABLE "dd_campaign_invitation";
                ALTER TABLE "dd_campaign_invitation_old" RENAME TO "dd_campaign_invitation";

                CREATE INDEX "IX_dd_campaign_invitation_CampaignId_Status"
                    ON "dd_campaign_invitation" ("CampaignId", "Status");
                CREATE UNIQUE INDEX "IX_dd_campaign_invitation_TokenHash"
                    ON "dd_campaign_invitation" ("TokenHash");
                CREATE UNIQUE INDEX "IX_dd_campaign_invitation_pending_participant"
                    ON "dd_campaign_invitation" ("ParticipantId")
                    WHERE "Status" = 0 AND "ParticipantId" IS NOT NULL;
                """);
            return;
        }

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
