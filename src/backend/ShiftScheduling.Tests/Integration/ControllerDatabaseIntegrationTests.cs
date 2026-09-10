using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Availabilities.Controllers;
using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.LeaveRequests.Controllers;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.LeaveRequests.Services;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Features.Reports.Controllers;
using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Controllers;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Features.WorkSchedules.Controllers;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.Integration;

[Collection("SqlServerIntegration")]
public sealed class ControllerDatabaseIntegrationTests : IAsyncLifetime
{
    private readonly SqlServerIntegrationFixture _fixture;

    public ControllerDatabaseIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    // ============================================================
    // IT-01 – CREATE SCHEDULE PERIOD THROUGH CONTROLLER
    // ============================================================
    [Fact(DisplayName = "IT-01 - Tạo kỳ lịch qua SchedulePeriodController ghi đúng 7 ngày, 21 định mức ca và AuditLog vào database")]
    public async Task IT01_CreateSchedulePeriod_ThroughController_CreatesPeriodAndRequirementsInDatabase()
    {
        // 1. Precondition
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        await using var db = _fixture.CreateDbContext();

        var controller = CreateSchedulePeriodController(db, clock, accountId: 2, role: RoleNames.Manager);

        var weekStartDate = new DateOnly(2026, 10, 5); // Monday
        var deadline = new DateTime(2026, 10, 4, 23, 59, 0);

        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ lịch Tuần 41",
            WeekStartDate = weekStartDate,
            AvailabilityDeadline = deadline
        };

        // 2. Action through Controller
        var actionResult = await controller.Create(model, CancellationToken.None);

        // 3. Controller response assertion
        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(SchedulePeriodController.Index), redirect.ActionName);

        // 4. Query Database directly with fresh context to assert side-effects
        await using var queryDb = _fixture.CreateDbContext();
        var period = await queryDb.SchedulePeriods
            .Include(p => p.StaffingRequirements)
            .FirstOrDefaultAsync(p => p.WeekStartDate == weekStartDate);

        Assert.NotNull(period);
        Assert.Equal("Kỳ lịch Tuần 41", period.PeriodName);
        Assert.Equal(weekStartDate, period.WeekStartDate);
        Assert.Equal(weekStartDate.AddDays(6), period.WeekEndDate); // Exactly 7 days
        Assert.Equal(RegistrationStatus.Preparing, period.RegistrationStatus);
        Assert.Equal(2, period.CreatedByAccountId);

        // Assert exactly 21 StaffingRequirements (7 days x 3 shifts)
        Assert.Equal(21, period.StaffingRequirements.Count);
        Assert.All(period.StaffingRequirements, req =>
        {
            Assert.True(req.WorkDate >= weekStartDate && req.WorkDate <= weekStartDate.AddDays(6));
            Assert.Contains(req.ShiftTypeId, new[] { 1, 2, 3 });
            Assert.True(req.RequiredSalesStaff >= 0);
            Assert.True(req.RequiredCashiers >= 0);
        });

        // Assert AuditLog in Database
        var audit = await queryDb.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "CREATE_SCHEDULE_PERIOD" &&
            a.EntityName == nameof(SchedulePeriod) &&
            a.EntityId == period.PeriodName);

        Assert.NotNull(audit);
        Assert.Equal(2, audit.ActorAccountId);
    }

    // ============================================================
    // IT-02 – EMPLOYEE REGISTER AVAILABILITY THROUGH CONTROLLER
    // ============================================================
    [Fact(DisplayName = "IT-02 - Nhân viên đăng ký lịch rảnh qua AvailabilityController lưu đúng ca, preference và AuditLog vào database")]
    public async Task IT02_EmployeeRegisterAvailability_ThroughController_SavesAvailabilitiesAndAuditLogInDatabase()
    {
        // 1. Precondition: Create an open SchedulePeriod
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        var weekStartDate = new DateOnly(2026, 10, 12);

        await using var setupDb = _fixture.CreateDbContext();
        var period = await SeedSchedulePeriodWith21RequirementsAsync(setupDb, weekStartDate, clock);

        var controller = CreateAvailabilityController(setupDb, clock, accountId: 3, role: RoleNames.Employee);

        // Keys format expected by production: yyyy-MM-dd_ShiftTypeId
        var key1 = $"{weekStartDate:yyyy-MM-dd}_1"; // Monday Morning
        var key2 = $"{weekStartDate.AddDays(1):yyyy-MM-dd}_2"; // Tuesday Afternoon
        var selectedShiftKeys = new List<string> { key1, key2 };
        var shiftPreferences = new Dictionary<string, AvailabilityPreferenceLevel>
        {
            [key1] = AvailabilityPreferenceLevel.Preferred,
            [key2] = AvailabilityPreferenceLevel.Normal
        };

        // 2. Action through Controller
        var actionResult = await controller.Register(
            period.SchedulePeriodId, selectedShiftKeys, shiftPreferences, CancellationToken.None);

        // 3. Controller response assertion
        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(AvailabilityController.Index), redirect.ActionName);

        // 4. Query Database directly with fresh context to assert side-effects
        await using var queryDb = _fixture.CreateDbContext();
        var savedAvailabilities = await queryDb.Availabilities
            .Where(a => a.SchedulePeriodId == period.SchedulePeriodId && a.EmployeeId == 1)
            .OrderBy(a => a.WorkDate)
            .ToListAsync();

        Assert.Equal(2, savedAvailabilities.Count);

        Assert.Equal(weekStartDate, savedAvailabilities[0].WorkDate);
        Assert.Equal(1, savedAvailabilities[0].ShiftTypeId);
        Assert.Equal(AvailabilityPreferenceLevel.Preferred, savedAvailabilities[0].PreferenceLevel);

        Assert.Equal(weekStartDate.AddDays(1), savedAvailabilities[1].WorkDate);
        Assert.Equal(2, savedAvailabilities[1].ShiftTypeId);
        Assert.Equal(AvailabilityPreferenceLevel.Normal, savedAvailabilities[1].PreferenceLevel);

        // Assert AuditLog in Database
        var audit = await queryDb.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "SAVE_EMPLOYEE_AVAILABILITY" &&
            a.EntityName == nameof(Availability) &&
            a.ActorAccountId == 3);

        Assert.NotNull(audit);
    }

    // ============================================================
    // IT-03 – LEAVE REQUEST SUBMIT + MANAGER APPROVE THROUGH CONTROLLER
    // ============================================================
    [Fact(DisplayName = "IT-03 - Luồng gửi đơn xin nghỉ và Quản lý duyệt qua LeaveRequestController cập nhật trạng thái Approved và AuditLog trong database")]
    public async Task IT03_LeaveRequestWorkflow_SubmitAndApproveThroughController_UpdatesDatabaseStateAndAudit()
    {
        // 1. Precondition
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        var weekStartDate = new DateOnly(2026, 10, 19);

        await using var setupDb = _fixture.CreateDbContext();
        var period = await SeedSchedulePeriodWith21RequirementsAsync(setupDb, weekStartDate, clock);

        var leaveDate = weekStartDate.AddDays(1); // Tuesday

        // Bước 1: Nhân viên gửi đơn qua LeaveRequestController
        var empController = CreateLeaveRequestController(setupDb, clock, accountId: 3, role: RoleNames.Employee);
        var createModel = new LeaveRequestCreateViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            LeaveDate = leaveDate,
            ShiftTypeId = 1,
            Reason = "Nghỉ việc gia đình có việc đột xuất"
        };

        var createResult = await empController.Create(createModel, CancellationToken.None);
        var createRedirect = Assert.IsType<RedirectToActionResult>(createResult);
        Assert.Equal(nameof(LeaveRequestController.Index), createRedirect.ActionName);

        // Query Database directly: Check Pending status
        await using var queryDb1 = _fixture.CreateDbContext();
        var pendingLeave = await queryDb1.LeaveRequests
            .FirstOrDefaultAsync(l => l.SchedulePeriodId == period.SchedulePeriodId && l.EmployeeId == 1);

        Assert.NotNull(pendingLeave);
        Assert.Equal(LeaveRequestStatus.Pending, pendingLeave.Status);
        Assert.Equal(1, pendingLeave.EmployeeId);
        Assert.Equal(period.SchedulePeriodId, pendingLeave.SchedulePeriodId);
        Assert.Equal(leaveDate, pendingLeave.LeaveDate);
        Assert.Equal(1, pendingLeave.ShiftTypeId);

        var expectedEntityId = $"{pendingLeave.EmployeeId}:{pendingLeave.LeaveDate:yyyyMMdd}:{pendingLeave.ShiftTypeId?.ToString() ?? "DAY"}";
        var submitAudit = await queryDb1.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "SUBMIT_LEAVE_REQUEST" &&
            a.EntityName == nameof(LeaveRequest) &&
            a.EntityId == expectedEntityId);
        Assert.NotNull(submitAudit);
        Assert.Equal(3, submitAudit.ActorAccountId);

        // Bước 2: Quản lý duyệt đơn qua LeaveRequestController
        await using var managerDb = _fixture.CreateDbContext();
        var managerController = CreateLeaveRequestController(managerDb, clock, accountId: 2, role: RoleNames.Manager);

        var reviewModel = new LeaveRequestReviewViewModel
        {
            LeaveRequestId = pendingLeave.LeaveRequestId,
            ManagerNote = "Đã phê duyệt lý do chính đáng",
            RowVersion = Convert.ToBase64String(pendingLeave.RowVersion)
        };

        var reviewResult = await managerController.Review(
            pendingLeave.LeaveRequestId, reviewModel, approve: true, CancellationToken.None);

        var reviewRedirect = Assert.IsType<RedirectToActionResult>(reviewResult);
        Assert.Equal(nameof(LeaveRequestController.Index), reviewRedirect.ActionName);

        // Query Database directly: Check Approved status and reviewer metadata
        await using var queryDb2 = _fixture.CreateDbContext();
        var approvedLeave = await queryDb2.LeaveRequests.FindAsync(pendingLeave.LeaveRequestId);

        Assert.NotNull(approvedLeave);
        Assert.Equal(LeaveRequestStatus.Approved, approvedLeave.Status);
        Assert.Equal(2, approvedLeave.ApprovedByAccountId);
        Assert.NotNull(approvedLeave.ReviewedAt);
        Assert.Equal("Đã phê duyệt lý do chính đáng", approvedLeave.ManagerNote);

        var reviewAudit = await queryDb2.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "REVIEW_LEAVE_REQUEST" &&
            a.EntityName == nameof(LeaveRequest) &&
            a.EntityId == pendingLeave.LeaveRequestId.ToString());
        Assert.NotNull(reviewAudit);
        Assert.Equal(2, reviewAudit.ActorAccountId);
    }

    // ============================================================
    // IT-04 – MANUAL ASSIGNMENT THROUGH WORKSCHEDULE CONTROLLER
    // ============================================================
    [Fact(DisplayName = "IT-04 - Phân công thủ công qua WorkScheduleController lưu ShiftAssignment thật Source=Manual và AuditLog vào database")]
    public async Task IT04_ManualAssignment_ThroughWorkScheduleController_SavesAssignmentAndAuditToDatabase()
    {
        // 1. Precondition
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        var weekStartDate = new DateOnly(2026, 10, 26);
        var workDate = weekStartDate; // Monday

        await using var setupDb = _fixture.CreateDbContext();
        var period = await SeedSchedulePeriodWith21RequirementsAsync(setupDb, weekStartDate, clock);

        // Đăng ký lịch rảnh cho Employee 1 vào ca sáng thứ 2
        setupDb.Availabilities.Add(new Availability
        {
            EmployeeId = 1,
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = workDate,
            ShiftTypeId = 1,
            PreferenceLevel = AvailabilityPreferenceLevel.Preferred,
            RegisteredAt = clock.UtcNow
        });

        // Tạo sẵn WorkSchedule Draft
        var draftSchedule = new WorkSchedule
        {
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Draft,
            CreatedByAccountId = 2,
            CreatedAt = clock.UtcNow
        };
        setupDb.WorkSchedules.Add(draftSchedule);
        await setupDb.SaveChangesAsync();

        // 2. Action: Quản lý gọi WorkScheduleController.AssignManual
        await using var actionDb = _fixture.CreateDbContext();
        var controller = CreateWorkScheduleController(actionDb, clock, accountId: 2, role: RoleNames.Manager);

        var assignModel = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = workDate,
            ShiftTypeId = 1,
            EmployeeId = 1
        };

        var actionResult = await controller.AssignManual(assignModel, CancellationToken.None);

        // 3. Controller response assertion: JsonResult { success = true }
        var jsonResult = Assert.IsType<JsonResult>(actionResult);
        var json = JsonSerializer.Serialize(jsonResult.Value);
        using var jsonDoc = JsonDocument.Parse(json);
        Assert.True(jsonDoc.RootElement.GetProperty("success").GetBoolean());

        // 4. Query Database directly with fresh context
        await using var queryDb = _fixture.CreateDbContext();
        var assignment = await queryDb.ShiftAssignments
            .Include(a => a.WorkSchedule)
            .FirstOrDefaultAsync(a =>
                a.WorkSchedule.SchedulePeriodId == period.SchedulePeriodId &&
                a.EmployeeId == 1 &&
                a.WorkDate == workDate &&
                a.ShiftTypeId == 1);

        Assert.NotNull(assignment);
        Assert.Equal(AssignmentSource.Manual, assignment.Source);
        Assert.Equal(draftSchedule.WorkScheduleId, assignment.WorkScheduleId);

        // Assert AuditLog in Database
        var audit = await queryDb.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "MANUAL_ASSIGN_EMPLOYEE" &&
            a.EntityName == nameof(ShiftAssignment) &&
            a.ActorAccountId == 2);

        Assert.NotNull(audit);
    }

    // ============================================================
    // IT-05 – NC-07 ADJUST ACTUAL WORKED HOURS THROUGH REPORT CONTROLLER
    // ============================================================
    [Fact(DisplayName = "IT-05 - Điều chỉnh giờ công thực tế NC-07 qua ReportController cập nhật database và phản ánh vào báo cáo tổng hợp")]
    public async Task IT05_AdjustActualWorkedHours_ThroughReportController_UpdatesAssignmentAndReflectedInReport()
    {
        // 1. Precondition
        var weekStartDate = new DateOnly(2026, 11, 2);
        var shiftDate = weekStartDate; // Monday

        // Clock đặt sau khi ca đã kết thúc (Ca sáng 8:00 - 12:00 kết thúc vào trưa cùng ngày)
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc));

        await using var setupDb = _fixture.CreateDbContext();
        var period = await SeedSchedulePeriodWith21RequirementsAsync(setupDb, weekStartDate, clock);

        // Tạo WorkSchedule trạng thái Published
        var publishedSchedule = new WorkSchedule
        {
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Published,
            CreatedByAccountId = 2,
            PublishedByAccountId = 2,
            PublishedAt = clock.UtcNow.AddDays(-1),
            CreatedAt = clock.UtcNow.AddDays(-2)
        };
        setupDb.WorkSchedules.Add(publishedSchedule);

        // Thêm ShiftAssignment ca Sáng (DurationHours = 4h) cho Employee 1
        var assignment = new ShiftAssignment
        {
            WorkSchedule = publishedSchedule,
            EmployeeId = 1,
            WorkDate = shiftDate,
            ShiftTypeId = 1, // MORNING (4h)
            Source = AssignmentSource.Manual,
            CreatedAt = clock.UtcNow.AddDays(-2)
        };
        setupDb.ShiftAssignments.Add(assignment);
        await setupDb.SaveChangesAsync();

        // 2. Action: Quản lý gọi ReportController.AdjustWorkdayHours điều chỉnh từ 4h lên 5h
        await using var actionDb = _fixture.CreateDbContext();
        var controller = CreateReportController(actionDb, clock, accountId: 2, role: RoleNames.Manager);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = period.SchedulePeriodId,
            ShiftAssignmentId = assignment.ShiftAssignmentId,
            ActualWorkedHours = 5m,
            WorkdayAdjustmentReason = "Nhân viên tăng ca thêm 1 tiếng",
            IsReset = false
        };

        var actionResult = await controller.AdjustWorkdayHours(request, CancellationToken.None);

        // 3. Controller response assertion
        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(ReportController.Index), redirect.ActionName);

        // 4. Query Database directly with fresh context to assert side-effects
        await using var queryDb = _fixture.CreateDbContext();
        var updatedAssignment = await queryDb.ShiftAssignments.FindAsync(assignment.ShiftAssignmentId);

        Assert.NotNull(updatedAssignment);
        Assert.Equal(5m, updatedAssignment.ActualWorkedHours);
        Assert.Equal("Nhân viên tăng ca thêm 1 tiếng", updatedAssignment.WorkdayAdjustmentReason);

        // Assert AuditLog
        var audit = await queryDb.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "ADJUST_WORKDAY_HOURS" &&
            a.EntityName == nameof(ShiftAssignment) &&
            a.EntityId == assignment.ShiftAssignmentId.ToString());

        Assert.NotNull(audit);
        Assert.Equal(2, audit.ActorAccountId);

        // 5. Read back through real WorkReportService to verify report aggregation side-effect
        var reportService = CreateRealWorkReportService(queryDb, clock);
        var overview = await reportService.GetManagerReportOverviewAsync(period.SchedulePeriodId);

        Assert.NotNull(overview);
        var empRow = Assert.Single(overview.Rows, r => r.EmployeeId == 1);
        Assert.Equal(5m, empRow.TotalHours); // Phản ánh 5h sau điều chỉnh, không phải 4h kế hoạch

        var detail = Assert.Single(overview.Details, d => d.ShiftAssignmentId == assignment.ShiftAssignmentId);
        Assert.Equal(4m, detail.PlannedHours);
        Assert.Equal(5m, detail.EffectiveWorkedHours);
        Assert.True(detail.IsManagerAdjusted);
        Assert.Equal("Nhân viên tăng ca thêm 1 tiếng", detail.AdjustmentReason);
    }

    // ============================================================
    // CONTROLLER & SERVICE FACTORY HELPERS
    // ============================================================

    private static SchedulePeriodController CreateSchedulePeriodController(
        ApplicationDbContext db, IClock clock, int accountId, string role)
    {
        var spRepo = new SchedulePeriodRepository(db);
        var stRepo = new ShiftTypeRepository(db);
        var notifRepo = new NotificationRepository(db);
        var auditRepo = new AuditLogRepository(db);
        var uow = new ApplicationUnitOfWork(db);
        var logger = NullLogger<SchedulePeriodService>.Instance;

        var service = new SchedulePeriodService(
            spRepo, stRepo, notifRepo, auditRepo, uow, clock, logger);

        var controller = new SchedulePeriodController(service, clock)
        {
            ControllerContext = SqlServerIntegrationFixture.CreateControllerContext(accountId, role)
        };
        controller.TempData = SqlServerIntegrationFixture.CreateTempData(controller.ControllerContext);
        return controller;
    }

    private static AvailabilityController CreateAvailabilityController(
        ApplicationDbContext db, IClock clock, int accountId, string role)
    {
        var availRepo = new AvailabilityRepository(db);
        var spRepo = new SchedulePeriodRepository(db);
        var stRepo = new ShiftTypeRepository(db);
        var empRepo = new EmployeeRepository(db);
        var auditRepo = new AuditLogRepository(db);
        var weeklyRepo = new WeeklyAvailabilityPatternRepository(db);
        var uow = new ApplicationUnitOfWork(db);
        var logger = NullLogger<AvailabilityService>.Instance;

        var service = new AvailabilityService(
            availRepo, spRepo, stRepo, empRepo, auditRepo, weeklyRepo, uow, clock, logger);

        var controller = new AvailabilityController(service)
        {
            ControllerContext = SqlServerIntegrationFixture.CreateControllerContext(accountId, role)
        };
        controller.TempData = SqlServerIntegrationFixture.CreateTempData(controller.ControllerContext);
        return controller;
    }

    private static LeaveRequestController CreateLeaveRequestController(
        ApplicationDbContext db, IClock clock, int accountId, string role)
    {
        var leaveRepo = new LeaveRequestRepository(db);
        var notifRepo = new NotificationRepository(db);
        var auditRepo = new AuditLogRepository(db);
        var uow = new ApplicationUnitOfWork(db);
        var logger = NullLogger<LeaveRequestService>.Instance;

        var service = new LeaveRequestService(
            leaveRepo, notifRepo, auditRepo, uow, clock, logger);

        var controller = new LeaveRequestController(service)
        {
            ControllerContext = SqlServerIntegrationFixture.CreateControllerContext(accountId, role)
        };
        controller.TempData = SqlServerIntegrationFixture.CreateTempData(controller.ControllerContext);
        return controller;
    }

    private static WorkScheduleController CreateWorkScheduleController(
        ApplicationDbContext db, IClock clock, int accountId, string role)
    {
        var wsRepo = new WorkScheduleRepository(db);
        var spRepo = new SchedulePeriodRepository(db);
        var stRepo = new ShiftTypeRepository(db);
        var empRepo = new EmployeeRepository(db);
        var availRepo = new AvailabilityRepository(db);
        var leaveRepo = new LeaveRequestRepository(db);
        var valService = new SchedulingValidationService();
        var orTools = new OrToolsSchedulingService(valService, NullLogger<OrToolsSchedulingService>.Instance);
        var notifRepo = new NotificationRepository(db);
        var auditRepo = new AuditLogRepository(db);
        var uow = new ApplicationUnitOfWork(db);
        var logger = NullLogger<WorkScheduleService>.Instance;

        var service = new WorkScheduleService(
            wsRepo, spRepo, stRepo, empRepo, availRepo, leaveRepo, valService, orTools, notifRepo, auditRepo, uow, clock, logger);

        var controller = new WorkScheduleController(service)
        {
            ControllerContext = SqlServerIntegrationFixture.CreateControllerContext(accountId, role)
        };
        controller.TempData = SqlServerIntegrationFixture.CreateTempData(controller.ControllerContext);
        return controller;
    }

    private static ReportController CreateReportController(
        ApplicationDbContext db, IClock clock, int accountId, string role)
    {
        var service = CreateRealWorkReportService(db, clock);

        var controller = new ReportController(service)
        {
            ControllerContext = SqlServerIntegrationFixture.CreateControllerContext(accountId, role)
        };
        controller.TempData = SqlServerIntegrationFixture.CreateTempData(controller.ControllerContext);
        return controller;
    }

    private static WorkReportService CreateRealWorkReportService(ApplicationDbContext db, IClock clock)
    {
        var wsRepo = new WorkScheduleRepository(db);
        var spRepo = new SchedulePeriodRepository(db);
        var empRepo = new EmployeeRepository(db);
        var leaveRepo = new LeaveRequestRepository(db);
        var auditRepo = new AuditLogRepository(db);
        var uow = new ApplicationUnitOfWork(db);

        return new WorkReportService(
            wsRepo, spRepo, empRepo, leaveRepo, auditRepo, uow, clock);
    }

    private static async Task<SchedulePeriod> SeedSchedulePeriodWith21RequirementsAsync(
        ApplicationDbContext db, DateOnly weekStartDate, IClock clock)
    {
        var weekEndDate = weekStartDate.AddDays(6);
        var deadline = weekStartDate.AddDays(-1).ToDateTime(new TimeOnly(23, 59));

        var period = new SchedulePeriod
        {
            PeriodName = $"Kỳ lịch {weekStartDate:dd/MM/yyyy}",
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            AvailabilityDeadline = clock.ToUtcFromVietnamTime(deadline),
            RegistrationStatus = RegistrationStatus.Open,
            CreatedByAccountId = 2,
            CreatedAt = clock.UtcNow
        };

        db.SchedulePeriods.Add(period);

        // Tạo 21 StaffingRequirement (7 ngày x 3 ca)
        for (int d = 0; d < 7; d++)
        {
            var date = weekStartDate.AddDays(d);
            for (int shiftId = 1; shiftId <= 3; shiftId++)
            {
                period.StaffingRequirements.Add(new StaffingRequirement
                {
                    WorkDate = date,
                    ShiftTypeId = shiftId,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1
                });
            }
        }

        await db.SaveChangesAsync();
        return period;
    }
}
