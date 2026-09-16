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
using ShiftScheduling.Features.Reports.Controllers;
using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Features.SchedulePeriods.Controllers;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Features.WorkSchedules.Controllers;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.Integration;

[Collection("SqlServerIntegration")]
public sealed partial class ControllerDatabaseIntegrationTests : IAsyncLifetime
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
