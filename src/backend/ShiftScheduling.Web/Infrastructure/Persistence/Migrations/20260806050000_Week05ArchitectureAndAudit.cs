using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiftScheduling.Infrastructure.Persistence;

#nullable disable

namespace ShiftScheduling.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260806050000_Week05ArchitectureAndAudit")]
public sealed class Week05ArchitectureAndAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ActorAccountId = table.Column<int>(type: "int", nullable: false),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PreviousValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.AuditLogId);
                table.ForeignKey(
                    name: "FK_AuditLogs_UserAccounts_ActorAccountId",
                    column: x => x.ActorAccountId,
                    principalTable: "UserAccounts",
                    principalColumn: "UserAccountId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ActorAccountId_CreatedAtUtc",
            table: "AuditLogs",
            columns: new[] { "ActorAccountId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EntityName_EntityId_CreatedAtUtc",
            table: "AuditLogs",
            columns: new[] { "EntityName", "EntityId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_Employees_Email",
            table: "Employees",
            column: "Email",
            unique: true,
            filter: "[Email] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditLogs");
        migrationBuilder.DropIndex(name: "IX_Employees_Email", table: "Employees");
    }
}
