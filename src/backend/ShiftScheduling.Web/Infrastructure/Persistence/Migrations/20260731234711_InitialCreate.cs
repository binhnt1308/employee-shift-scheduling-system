using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable IDE0161

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShiftScheduling.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftTypes",
                columns: table => new
                {
                    ShiftTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShiftName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DurationHours = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTypes", x => x.ShiftTypeId);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    UserAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByAccountId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.UserAccountId);
                    table.ForeignKey(
                        name: "FK_UserAccounts_UserAccounts_CreatedByAccountId",
                        column: x => x.CreatedByAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountId = table.Column<int>(type: "int", nullable: true),
                    EmployeeCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_Employees_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiverAccountId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RelatedObjectId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_UserAccounts_ReceiverAccountId",
                        column: x => x.ReceiverAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulePeriods",
                columns: table => new
                {
                    SchedulePeriodId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WeekEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AvailabilityDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistrationStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedByAccountId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulePeriods", x => x.SchedulePeriodId);
                    table.ForeignKey(
                        name: "FK_SchedulePeriods_UserAccounts_CreatedByAccountId",
                        column: x => x.CreatedByAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Availabilities",
                columns: table => new
                {
                    AvailabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SchedulePeriodId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Availabilities", x => x.AvailabilityId);
                    table.ForeignKey(
                        name: "FK_Availabilities_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Availabilities_SchedulePeriods_SchedulePeriodId",
                        column: x => x.SchedulePeriodId,
                        principalTable: "SchedulePeriods",
                        principalColumn: "SchedulePeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Availabilities_ShiftTypes_ShiftTypeId",
                        column: x => x.ShiftTypeId,
                        principalTable: "ShiftTypes",
                        principalColumn: "ShiftTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    LeaveRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SchedulePeriodId = table.Column<int>(type: "int", nullable: false),
                    LeaveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftTypeId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByAccountId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManagerNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.LeaveRequestId);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_SchedulePeriods_SchedulePeriodId",
                        column: x => x.SchedulePeriodId,
                        principalTable: "SchedulePeriods",
                        principalColumn: "SchedulePeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_ShiftTypes_ShiftTypeId",
                        column: x => x.ShiftTypeId,
                        principalTable: "ShiftTypes",
                        principalColumn: "ShiftTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_UserAccounts_ApprovedByAccountId",
                        column: x => x.ApprovedByAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffingRequirements",
                columns: table => new
                {
                    StaffingRequirementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchedulePeriodId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    RequiredSalesStaff = table.Column<int>(type: "int", nullable: false),
                    RequiredCashiers = table.Column<int>(type: "int", nullable: false),
                    MinimumLevel = table.Column<int>(type: "int", nullable: false),
                    RequiredExperiencedStaff = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffingRequirements", x => x.StaffingRequirementId);
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_SchedulePeriods_SchedulePeriodId",
                        column: x => x.SchedulePeriodId,
                        principalTable: "SchedulePeriods",
                        principalColumn: "SchedulePeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffingRequirements_ShiftTypes_ShiftTypeId",
                        column: x => x.ShiftTypeId,
                        principalTable: "ShiftTypes",
                        principalColumn: "ShiftTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    WorkScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchedulePeriodId = table.Column<int>(type: "int", nullable: false),
                    ScheduleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsGeneratedByORTools = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByAccountId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByAccountId = table.Column<int>(type: "int", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.WorkScheduleId);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_SchedulePeriods_SchedulePeriodId",
                        column: x => x.SchedulePeriodId,
                        principalTable: "SchedulePeriods",
                        principalColumn: "SchedulePeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_UserAccounts_CreatedByAccountId",
                        column: x => x.CreatedByAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_UserAccounts_PublishedByAccountId",
                        column: x => x.PublishedByAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    ShiftAssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkScheduleId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.ShiftAssignmentId);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_ShiftTypes_ShiftTypeId",
                        column: x => x.ShiftTypeId,
                        principalTable: "ShiftTypes",
                        principalColumn: "ShiftTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalTable: "WorkSchedules",
                        principalColumn: "WorkScheduleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftTransferRequests",
                columns: table => new
                {
                    ShiftTransferRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftAssignmentId = table.Column<int>(type: "int", nullable: false),
                    SenderEmployeeId = table.Column<int>(type: "int", nullable: false),
                    ReceiverEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReceiverStatus = table.Column<int>(type: "int", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiverRespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByAccountId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManagerNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTransferRequests", x => x.ShiftTransferRequestId);
                    table.ForeignKey(
                        name: "FK_ShiftTransferRequests_Employees_ReceiverEmployeeId",
                        column: x => x.ReceiverEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftTransferRequests_Employees_SenderEmployeeId",
                        column: x => x.SenderEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftTransferRequests_ShiftAssignments_ShiftAssignmentId",
                        column: x => x.ShiftAssignmentId,
                        principalTable: "ShiftAssignments",
                        principalColumn: "ShiftAssignmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftTransferRequests_UserAccounts_ApprovedByAccountId",
                        column: x => x.ApprovedByAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "UserAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ShiftTypes",
                columns: new[] { "ShiftTypeId", "DisplayOrder", "DurationHours", "EndTime", "IsActive", "ShiftCode", "ShiftName", "StartTime" },
                values: new object[,]
                {
                    { 1, 1, 4m, new TimeOnly(12, 0, 0), true, "MORNING", "Ca sáng", new TimeOnly(8, 0, 0) },
                    { 2, 2, 4m, new TimeOnly(16, 0, 0), true, "AFTERNOON", "Ca chiều", new TimeOnly(12, 0, 0) },
                    { 3, 3, 4m, new TimeOnly(20, 0, 0), true, "EVENING", "Ca tối", new TimeOnly(16, 0, 0) }
                });

            migrationBuilder.InsertData(
                table: "UserAccounts",
                columns: new[] { "UserAccountId", "CreatedAt", "CreatedByAccountId", "IsLocked", "PasswordHash", "Role", "Username" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, "100000.ABEiM0RVZneImaq7zN3u/w==.voGHdh8BUEr7qGOgntpkfAlsfKyMx/CzNR/DLboeAU4=", 1, "admin" },
                    { 2, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, false, "100000./+7dzLuqmYh3ZlVEMyIRAA==.UWV+wqzCmlDi0tlbWmy/DpHdbaaFtpczTXzp5MErAog=", 2, "quanly" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Availabilities_EmployeeId_SchedulePeriodId_WorkDate_ShiftTypeId",
                table: "Availabilities",
                columns: new[] { "EmployeeId", "SchedulePeriodId", "WorkDate", "ShiftTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Availabilities_SchedulePeriodId",
                table: "Availabilities",
                column: "SchedulePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Availabilities_ShiftTypeId",
                table: "Availabilities",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserAccountId",
                table: "Employees",
                column: "UserAccountId",
                unique: true,
                filter: "[UserAccountId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ApprovedByAccountId",
                table: "LeaveRequests",
                column: "ApprovedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_SchedulePeriodId_LeaveDate_ShiftTypeId",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "SchedulePeriodId", "LeaveDate", "ShiftTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_SchedulePeriodId",
                table: "LeaveRequests",
                column: "SchedulePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ShiftTypeId",
                table: "LeaveRequests",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReceiverAccountId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "ReceiverAccountId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePeriods_CreatedByAccountId",
                table: "SchedulePeriods",
                column: "CreatedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulePeriods_WeekStartDate",
                table: "SchedulePeriods",
                column: "WeekStartDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_EmployeeId",
                table: "ShiftAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftTypeId",
                table: "ShiftAssignments",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_WorkScheduleId_EmployeeId_WorkDate_ShiftTypeId",
                table: "ShiftAssignments",
                columns: new[] { "WorkScheduleId", "EmployeeId", "WorkDate", "ShiftTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTransferRequests_ApprovedByAccountId",
                table: "ShiftTransferRequests",
                column: "ApprovedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTransferRequests_ReceiverEmployeeId",
                table: "ShiftTransferRequests",
                column: "ReceiverEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTransferRequests_SenderEmployeeId",
                table: "ShiftTransferRequests",
                column: "SenderEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTransferRequests_ShiftAssignmentId",
                table: "ShiftTransferRequests",
                column: "ShiftAssignmentId");

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

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_SchedulePeriodId_WorkDate_ShiftTypeId",
                table: "StaffingRequirements",
                columns: new[] { "SchedulePeriodId", "WorkDate", "ShiftTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffingRequirements_ShiftTypeId",
                table: "StaffingRequirements",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_CreatedByAccountId",
                table: "UserAccounts",
                column: "CreatedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Username",
                table: "UserAccounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_CreatedByAccountId",
                table: "WorkSchedules",
                column: "CreatedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_PublishedByAccountId",
                table: "WorkSchedules",
                column: "PublishedByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_SchedulePeriodId",
                table: "WorkSchedules",
                column: "SchedulePeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Availabilities");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ShiftTransferRequests");

            migrationBuilder.DropTable(
                name: "StaffingRequirements");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "ShiftTypes");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "SchedulePeriods");

            migrationBuilder.DropTable(
                name: "UserAccounts");
        }
    }
}
