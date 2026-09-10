using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftScheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyAvailabilityPatternAndPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferenceLevel",
                table: "Availabilities",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "WeeklyAvailabilityPatterns",
                columns: table => new
                {
                    WeeklyAvailabilityPatternId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    ShiftCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PreferenceLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyAvailabilityPatterns", x => x.WeeklyAvailabilityPatternId);
                    table.CheckConstraint("CK_WeeklyAvailabilityPatterns_DayOfWeek", "[DayOfWeek] BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_WeeklyAvailabilityPatterns_PreferenceLevel", "[PreferenceLevel] IN (1, 2)");
                    table.CheckConstraint("CK_WeeklyAvailabilityPatterns_ShiftCode", "[ShiftCode] IN ('MORNING', 'AFTERNOON', 'EVENING')");
                    table.ForeignKey(
                        name: "FK_WeeklyAvailabilityPatterns_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Availabilities_PreferenceLevel",
                table: "Availabilities",
                sql: "[PreferenceLevel] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAvailabilityPatterns_EmployeeId_DayOfWeek_ShiftCode",
                table: "WeeklyAvailabilityPatterns",
                columns: new[] { "EmployeeId", "DayOfWeek", "ShiftCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyAvailabilityPatterns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Availabilities_PreferenceLevel",
                table: "Availabilities");

            migrationBuilder.DropColumn(
                name: "PreferenceLevel",
                table: "Availabilities");
        }
    }
}
