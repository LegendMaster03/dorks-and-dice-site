using System;
using dorks_and_dice_site.Services.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dorks_and_dice_site.Migrations.Identity;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260916065000_AddOperatorIdentity")]
public partial class AddOperatorIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AccountKind",
            table: "AspNetUsers",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "OperatorCredentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SecretPrefix = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperatorCredentials", x => x.Id);
                table.ForeignKey(
                    name: "FK_OperatorCredentials_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OperatorBrowserBootstraps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                IssuanceInvocationId = table.Column<Guid>(type: "uuid", nullable: false),
                SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperatorBrowserBootstraps", x => x.Id);
                table.ForeignKey(
                    name: "FK_OperatorBrowserBootstraps_OperatorCredentials_CredentialId",
                    column: x => x.CredentialId,
                    principalTable: "OperatorCredentials",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OperatorAuditRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvocationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                Client = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Capability = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Resource = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperatorAuditRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_OperatorAuditRecords_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OperatorAuditRecords_OperatorCredentials_CredentialId",
                    column: x => x.CredentialId,
                    principalTable: "OperatorCredentials",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OperatorBrowserBootstraps_CredentialId",
            table: "OperatorBrowserBootstraps",
            column: "CredentialId");
        migrationBuilder.CreateIndex(
            name: "IX_OperatorBrowserBootstraps_ExpiresAt",
            table: "OperatorBrowserBootstraps",
            column: "ExpiresAt");
        migrationBuilder.CreateIndex(
            name: "IX_OperatorBrowserBootstraps_IssuanceInvocationId",
            table: "OperatorBrowserBootstraps",
            column: "IssuanceInvocationId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_OperatorBrowserBootstraps_UserId",
            table: "OperatorBrowserBootstraps",
            column: "UserId");
        migrationBuilder.CreateIndex(
            name: "IX_OperatorCredentials_UserId",
            table: "OperatorCredentials",
            column: "UserId");
        migrationBuilder.CreateIndex(
            name: "IX_OperatorAuditRecords_CredentialId",
            table: "OperatorAuditRecords",
            column: "CredentialId");
        migrationBuilder.CreateIndex(
            name: "IX_OperatorAuditRecords_InvocationId",
            table: "OperatorAuditRecords",
            column: "InvocationId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_OperatorAuditRecords_UserId",
            table: "OperatorAuditRecords",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OperatorAuditRecords");
        migrationBuilder.DropTable(name: "OperatorBrowserBootstraps");
        migrationBuilder.DropTable(name: "OperatorCredentials");
        migrationBuilder.DropColumn(name: "AccountKind", table: "AspNetUsers");
    }
}
