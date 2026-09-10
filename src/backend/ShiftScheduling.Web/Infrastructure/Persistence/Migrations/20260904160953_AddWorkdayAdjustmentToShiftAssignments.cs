using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftScheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkdayAdjustmentToShiftAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualWorkedHours",
                table: "ShiftAssignments",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkdayAdjustmentReason",
                table: "ShiftAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualWorkedHours",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "WorkdayAdjustmentReason",
                table: "ShiftAssignments");
        }
    }
}
