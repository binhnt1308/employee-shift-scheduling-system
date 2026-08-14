using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftScheduling.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoEmployeeSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "UserAccounts",
                columns: new[] { "UserAccountId", "CreatedAt", "CreatedByAccountId", "IsLocked", "PasswordHash", "Role", "Username" },
                values: new object[] { 3, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, false, "100000.o2UkhUfzacTkDzOTs2NgPQ==.H/cGgTusNfv1PTAS9pTbBKEfNKMSw5IurN0GUEwEh90=", 3, "nhanvien" });

            migrationBuilder.InsertData(
                table: "Employees",
                columns: new[] { "EmployeeId", "DateOfBirth", "Email", "EmployeeCode", "EmploymentType", "FullName", "IsActive", "Level", "PhoneNumber", "Position", "StartDate", "UserAccountId" },
                values: new object[] { 1, new DateOnly(2000, 1, 1), "nhanvien@example.com", "EMP001", 2, "Nguyễn Văn Nhân Viên", true, 2, "0901234567", 1, new DateOnly(2026, 1, 1), 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Employees",
                keyColumn: "EmployeeId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "UserAccounts",
                keyColumn: "UserAccountId",
                keyValue: 3);
        }
    }
}
