using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.WorkSchedules.Controllers;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class WorkScheduleCloseLifecycleTests
{
    private readonly FakeWorkScheduleRepository _workScheduleRepo = new();
    private readonly FakeSchedulePeriodRepository _periodRepo = new();
    private readonly FakeShiftTypeRepository _shiftTypeRepo = new();
    private readonly FakeEmployeeRepository _employeeRepo = new();
    private readonly FakeAvailabilityRepository _availRepo = new();
    private readonly FakeLeaveRequestRepository _leaveRepo = new();
    private readonly FakeNotificationRepository _notificationRepo = new();
    private readonly FakeAuditLogRepository _auditLogRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly SchedulingValidationService _validationService = new();
    private readonly SchedulePeriod _period;
    private readonly WorkSchedule _schedule;
    private readonly ShiftType _morningShift;
    private readonly Employee _employee;

    public WorkScheduleCloseLifecycleTests()
    {
        _period = new SchedulePeriod
        {
            SchedulePeriodId = 1, PeriodName = "Kỳ tháng 9",
            WeekStartDate = new DateOnly(2026, 9, 7), WeekEndDate = new DateOnly(2026, 9, 13)
        };
        _morningShift = new ShiftType
        {
            ShiftTypeId = 1, ShiftCode = "M", ShiftName = "Sáng",
            StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4, IsActive = true
        };
        _employee = new Employee
        {
            EmployeeId = 1, UserAccountId = 101, EmployeeCode = "NV01", FullName = "Nguyễn An",
            Position = EmployeePosition.Sales, EmploymentType = EmploymentType.FullTime, IsActive = true
        };
        _schedule = new WorkSchedule
        {
            WorkScheduleId = 10, SchedulePeriodId = 1, SchedulePeriod = _period,
            Status = ScheduleStatus.Published, PublishedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
        };

        InitData();
    }

    private const string ValidRowVersion = "AQIDBAUGBwg=";

    private void InitData()
    {
        _periodRepo.SchedulePeriods.Add(_period);
        _period.WorkSchedules.Add(_schedule);
        _shiftTypeRepo.ShiftTypes.Add(_morningShift);
        _employeeRepo.Employees.Add(_employee);
        _workScheduleRepo.Schedules.Add(_schedule);
        _schedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 100, WorkScheduleId = 10, WorkSchedule = _schedule,
            EmployeeId = 1, Employee = _employee, WorkDate = new DateOnly(2026, 9, 12),
            ShiftTypeId = 1, ShiftType = _morningShift
        });
    }

    private WorkScheduleService CreateService(FakeClock clock) => new(
        _workScheduleRepo, _periodRepo, _shiftTypeRepo, _employeeRepo, _availRepo, _leaveRepo, _validationService,
        new OrToolsSchedulingService(_validationService, NullLogger<OrToolsSchedulingService>.Instance),
        _notificationRepo, _auditLogRepo, _unitOfWork, clock, NullLogger<WorkScheduleService>.Instance);

    private WorkReportService CreateReportService(FakeClock clock) => new(
        _workScheduleRepo, _periodRepo, _employeeRepo, _leaveRepo, _auditLogRepo, _unitOfWork, clock);

    [Fact(DisplayName = "TC-LC-101: Draft schedule -> StatusName 'Bản nháp'")]
    public void DerivedStatus_Draft_ReturnsBanNhap()
    {
        _schedule.Status = ScheduleStatus.Draft;
        var clock = new FakeClock(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            _period, _schedule, clock.UtcNow, new DateOnly(2026, 9, 1));
        Assert.Equal("Bản nháp", result.StatusName);
    }

    [Fact(DisplayName = "TC-LC-102: Published trước ngày bắt đầu -> StatusName 'Đã công bố'")]
    public void DerivedStatus_PublishedBeforeStart_ReturnsDaCongBo()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            _period, _schedule, clock.UtcNow, new DateOnly(2026, 9, 1));
        Assert.Equal("Đã công bố", result.StatusName);
    }

    [Fact(DisplayName = "TC-LC-103: Published trong thời gian kỳ -> StatusName 'Đang áp dụng'")]
    public void DerivedStatus_PublishedDuringPeriod_ReturnsDangApDung()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));
        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            _period, _schedule, clock.UtcNow, new DateOnly(2026, 9, 10));
        Assert.Equal("Đang áp dụng", result.StatusName);
    }

    [Fact(DisplayName =
        "TC-LC-104: Published sau khi hết tuần nhưng chưa chốt -> Vẫn StatusName 'Đang áp dụng'")]
    public void DerivedStatus_PublishedAfterWeekEnd_ReturnsDangApDung()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            _period, _schedule, clock.UtcNow, new DateOnly(2026, 9, 14));
        Assert.Equal("Đang áp dụng", result.StatusName);
    }

    [Fact(DisplayName = "TC-LC-105: Closed schedule -> StatusName 'Đã chốt'")]
    public void DerivedStatus_Closed_ReturnsDaChot()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var clock = new FakeClock(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            _period, _schedule, clock.UtcNow, new DateOnly(2026, 9, 15));
        Assert.Equal("Đã chốt", result.StatusName);
    }

    [Fact(DisplayName = "TC-LC-106: Chốt kỳ Published trước ngày bắt đầu -> Từ chối")]
    public async Task CloseSchedule_PublishedBeforeStart_ReturnsInvalidScheduleStatus()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.CloseScheduleAsync(1, ValidRowVersion, 99);
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidScheduleStatus, result.ErrorCode);
        Assert.Equal(ScheduleStatus.Published, _schedule.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_auditLogRepo.Items);
    }

    [Fact(DisplayName = "TC-LC-107: Chốt kỳ Published trong khi kỳ đang diễn ra -> Từ chối")]
    public async Task CloseSchedule_PublishedDuringPeriod_ReturnsInvalidScheduleStatus()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.CloseScheduleAsync(1, ValidRowVersion, 99);
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidScheduleStatus, result.ErrorCode);
        Assert.Equal(ScheduleStatus.Published, _schedule.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_auditLogRepo.Items);
    }

    [Fact(DisplayName =
        "TC-LC-108: Chốt kỳ Published sau khi kỳ kết thúc -> Thành công và đổi Status thành Closed")]
    public async Task CloseSchedule_PublishedAfterWeekEnd_SucceedsAndSetsClosed()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.CloseScheduleAsync(1, ValidRowVersion, 99);
        Assert.True(result.Succeeded);
        Assert.Equal(ScheduleStatus.Closed, _schedule.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC-LC-109: Chốt kỳ đã chốt -> Từ chối với InvalidScheduleStatus")]
    public async Task CloseSchedule_AlreadyClosed_ReturnsInvalidScheduleStatus()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.CloseScheduleAsync(1, ValidRowVersion, 99);
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidScheduleStatus, result.ErrorCode);
        Assert.Equal(ScheduleStatus.Closed, _schedule.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_auditLogRepo.Items);
    }

    [Fact(DisplayName = "TC-LC-110: Chốt kỳ ở trạng thái Nháp -> Từ chối với InvalidScheduleStatus")]
    public async Task CloseSchedule_Draft_ReturnsInvalidScheduleStatus()
    {
        _schedule.Status = ScheduleStatus.Draft;
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.CloseScheduleAsync(1, ValidRowVersion, 99);
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidScheduleStatus, result.ErrorCode);
        Assert.Equal(ScheduleStatus.Draft, _schedule.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_auditLogRepo.Items);
    }

    [Fact(DisplayName = "TC-LC-114: Chốt kỳ với RowVersion rỗng/null -> Từ chối ConcurrencyConflict")]
    public async Task CloseSchedule_EmptyRowVersion_ReturnsConcurrencyConflict()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.CloseScheduleAsync(1, "", 99);
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Equal(ScheduleStatus.Published, _schedule.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC-LC-115: Chốt kỳ với RowVersion sai định dạng Base64 -> Từ chối ConcurrencyConflict")]
    public async Task CloseSchedule_InvalidRowVersion_ReturnsConcurrencyConflict()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.CloseScheduleAsync(1, "invalid_base64!", 99);
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Equal(ScheduleStatus.Published, _schedule.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName =
        "TC-LC-116: Chốt kỳ với RowVersion bị stale -> Từ chối ConcurrencyConflict và không đổi trạng thái")]
    public async Task CloseSchedule_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);

        var staleRowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });
        var result = await service.CloseScheduleAsync(1, staleRowVersion, 99);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Equal(ScheduleStatus.Published, _schedule.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_auditLogRepo.Items);
    }

    [Fact(DisplayName =
        "TC-LC-111: Điều chỉnh giờ công trên kỳ đã chốt -> Từ chối và 0 SaveChanges")]
    public async Task AdjustWorkdayHours_ClosedSchedule_RejectsAndZeroSave()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var reportService = CreateReportService(clock);
        var req = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1, ShiftAssignmentId = 100,
            ActualWorkedHours = 5m, WorkdayAdjustmentReason = "Làm thêm 1h"
        };
        var result = await reportService.AdjustWorkdayHoursAsync(99, req);
        Assert.False(result.Succeeded);
        Assert.Equal("ScheduleClosed", result.ErrorCode);
        Assert.Null(_schedule.ShiftAssignments.First().ActualWorkedHours);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact(DisplayName =
        "TC-LC-112: Reset giờ công trên kỳ đã chốt -> Từ chối và không đổi dữ liệu gốc")]
    public async Task AdjustWorkdayHoursReset_ClosedSchedule_RejectsAndUnchanged()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var assignment = _schedule.ShiftAssignments.First();
        assignment.ActualWorkedHours = 5m;
        assignment.WorkdayAdjustmentReason = "Sửa cũ";

        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var reportService = CreateReportService(clock);
        var req = new AdjustWorkdayHoursRequest { SchedulePeriodId = 1, ShiftAssignmentId = 100, IsReset = true };
        var result = await reportService.AdjustWorkdayHoursAsync(99, req);
        Assert.False(result.Succeeded);
        Assert.Equal("ScheduleClosed", result.ErrorCode);
        Assert.Equal(5m, assignment.ActualWorkedHours);
        Assert.Equal("Sửa cũ", assignment.WorkdayAdjustmentReason);
    }

    [Fact(DisplayName = "TC-LC-113: Action Close trong Controller -> Có ValidateAntiForgeryToken và Manager Auth")]
    public async Task Controller_CloseEndpoint_AuthorizedPostRedirectsToEdit()
    {
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var controller = new WorkScheduleController(service)
        {
            ControllerContext = new() { HttpContext = CreateHttpContext(99) },
            TempData = new TempDataDictionary(CreateHttpContext(99), new TestTempDataProvider())
        };

        var result = await controller.Close(1, ValidRowVersion, CancellationToken.None);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(ScheduleStatus.Closed, _schedule.Status);
    }

    [Fact(DisplayName = "TC-LC-117: Manager Work Report - Đọc được lịch ở trạng thái Closed")]
    public async Task GetWorkReport_ClosedSchedule_ReturnsReportData()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var reportService = CreateReportService(clock);
        var report = await reportService.GetManagerReportOverviewAsync(1, null, null, null, CancellationToken.None);
        Assert.NotNull(report);
        Assert.Equal("Kỳ tháng 9", report.PeriodName);
    }

    [Fact(DisplayName = "TC-LC-118: Employee MyReport - Nhân viên đọc được báo cáo trên lịch Closed")]
    public async Task GetEmployeeReport_ClosedSchedule_ReturnsEmployeeReportData()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var reportService = CreateReportService(clock);
        var report = await reportService.GetEmployeeOwnReportAsync(101, 1, CancellationToken.None);
        Assert.NotNull(report);
        Assert.Equal("Nguyễn An", report.Summary?.FullName);
    }

    [Fact(DisplayName = "TC-LC-119: CSV Export - Xuất được file CSV từ lịch ở trạng thái Closed")]
    public async Task ExportWorkReportCsv_ClosedSchedule_ReturnsCsvBytes()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var reportService = CreateReportService(clock);
        var csvBytes = await reportService.GenerateReportCsvAsync(1, null, null, null, CancellationToken.None);
        Assert.NotNull(csvBytes);
        Assert.NotEmpty(csvBytes);
    }

    [Fact(DisplayName = "TC-LC-120: Historical MySchedule - Chọn kỳ đã chốt hiển thị được lịch Closed")]
    public async Task GetEmployeeMySchedule_ExplicitClosedPeriod_ReturnsClosedSchedule()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var clock = new FakeClock(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.GetEmployeeMyScheduleAsync(101, 1, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(1, result.SchedulePeriodId);
        Assert.Equal(ScheduleStatus.Closed, result.Status);
    }

    [Fact(DisplayName = "TC-LC-121: Default MySchedule - Mặc định ưu tiên kỳ Published hiện tại, không chọn kỳ Closed")]
    public async Task GetEmployeeMySchedule_DefaultNoPeriod_PrefersPublishedCurrentOverClosed()
    {
        _schedule.Status = ScheduleStatus.Closed;
        var currentPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 2, PeriodName = "Kỳ hiện tại",
            WeekStartDate = new DateOnly(2026, 9, 14), WeekEndDate = new DateOnly(2026, 9, 20)
        };
        var currentPublished = new WorkSchedule
        {
            WorkScheduleId = 20, SchedulePeriodId = 2,
            Status = ScheduleStatus.Published,
            SchedulePeriod = currentPeriod
        };
        _periodRepo.SchedulePeriods.Add(currentPeriod);
        _workScheduleRepo.Schedules.Add(currentPublished);

        var clock = new FakeClock(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc));
        var service = CreateService(clock);
        var result = await service.GetEmployeeMyScheduleAsync(101, null, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, result.SchedulePeriodId);
        Assert.Equal(ScheduleStatus.Published, result.Status);
    }

    private static HttpContext CreateHttpContext(int accountId)
    {
        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(ClaimTypes.Role, RoleNames.Manager)
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return context;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class ThrowingConcurrencyUnitOfWork : ShiftScheduling.Infrastructure.Persistence.IUnitOfWork
    {
        public int SaveCount => 0;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Stale row version.");
        }
    }
}
