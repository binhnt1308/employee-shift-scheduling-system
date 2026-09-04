using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftScheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyShiftPolicyToShiftTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowSplitShift",
                table: "ShiftTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxShiftsPerEmployeePerDay",
                table: "ShiftTypes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE [ShiftTypes] SET [MaxShiftsPerEmployeePerDay] = 2, [AllowSplitShift] = 0;");

            migrationBuilder.UpdateData(
                table: "ShiftTypes",
                keyColumn: "ShiftTypeId",
                keyValue: 1,
                column: "MaxShiftsPerEmployeePerDay",
                value: 2);

            migrationBuilder.UpdateData(
                table: "ShiftTypes",
                keyColumn: "ShiftTypeId",
                keyValue: 2,
                column: "MaxShiftsPerEmployeePerDay",
                value: 2);

            migrationBuilder.UpdateData(
                table: "ShiftTypes",
                keyColumn: "ShiftTypeId",
                keyValue: 3,
                column: "MaxShiftsPerEmployeePerDay",
                value: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShiftTypes_MaxShiftsPerEmployeePerDay",
                table: "ShiftTypes",
                sql: "[MaxShiftsPerEmployeePerDay] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShiftTypes_MaxShiftsPerEmployeePerDay",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "AllowSplitShift",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "MaxShiftsPerEmployeePerDay",
                table: "ShiftTypes");
        }
    }
}
