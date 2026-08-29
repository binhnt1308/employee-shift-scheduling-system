using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftScheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftConfigurationVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftTypes_DisplayOrder",
                table: "ShiftTypes");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTypes_ShiftCode",
                table: "ShiftTypes");

            migrationBuilder.AddColumn<int>(
                name: "ConfigurationVersion",
                table: "ShiftTypes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "ShiftTypes",
                keyColumn: "ShiftTypeId",
                keyValue: 1,
                column: "ConfigurationVersion",
                value: 1);

            migrationBuilder.UpdateData(
                table: "ShiftTypes",
                keyColumn: "ShiftTypeId",
                keyValue: 2,
                column: "ConfigurationVersion",
                value: 1);

            migrationBuilder.UpdateData(
                table: "ShiftTypes",
                keyColumn: "ShiftTypeId",
                keyValue: 3,
                column: "ConfigurationVersion",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_DisplayOrder_ConfigurationVersion",
                table: "ShiftTypes",
                columns: new[] { "DisplayOrder", "ConfigurationVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_ShiftCode_ConfigurationVersion",
                table: "ShiftTypes",
                columns: new[] { "ShiftCode", "ConfigurationVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM [ShiftTypes]
    WHERE [ConfigurationVersion] <> 1
)
BEGIN
    THROW 51000,
        'Cannot roll back shift configuration versioning after newer versions have been created.',
        1;
END;");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTypes_DisplayOrder_ConfigurationVersion",
                table: "ShiftTypes");

            migrationBuilder.DropIndex(
                name: "IX_ShiftTypes_ShiftCode_ConfigurationVersion",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "ConfigurationVersion",
                table: "ShiftTypes");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_DisplayOrder",
                table: "ShiftTypes",
                column: "DisplayOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_ShiftCode",
                table: "ShiftTypes",
                column: "ShiftCode",
                unique: true);
        }
    }
}
