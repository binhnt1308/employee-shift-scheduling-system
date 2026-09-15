using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.Controllers;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class WorkScheduleLeaveReplacementTests
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
    private readonly FakeClock _clock = new(new DateTime(2026, 8, 10, 7, 0, 0, DateTimeKind.Utc));
    private readonly SchedulingValidationService _validationService = new();
    private readonly WorkScheduleService _scheduleService;
    private readonly SchedulePeriod _period;
    private readonly ShiftType _morningShift, _afternoonShift, _eveningShift;
    private readonly Employee _leavingSales, _candidatePreferred, _candidateNormal, _candidateCashier;
    private readonly DateOnly _workDate = new(2026, 8, 12);

    public WorkScheduleLeaveReplacementTests()
    {
        _period = new SchedulePeriod { SchedulePeriodId = 1, PeriodName = "Kỳ lịch 1",
            WeekStartDate = new DateOnly(2026, 8, 10), WeekEndDate = new DateOnly(2026, 8, 16) };
        _morningShift = CreateShift(1, ShiftTypeConstants.MorningCode, "Sáng", 8, 12, 1);
        _afternoonShift = CreateShift(2, ShiftTypeConstants.AfternoonCode, "Chiều", 13, 17, 2);
        _eveningShift = CreateShift(3, ShiftTypeConstants.EveningCode, "Tối", 18, 22, 3);
        _period.StaffingRequirements = new[] { _morningShift, _afternoonShift, _eveningShift }
            .Select(s => new StaffingRequirement { SchedulePeriodId = 1, WorkDate = _workDate,
                ShiftTypeId = s.ShiftTypeId, RequiredSalesStaff = 1, RequiredCashiers = 1, ShiftType = s }).ToList();
        _leavingSales = CreateEmployee(1, "NV01", "Nghỉ", EmployeePosition.Sales, EmployeeLevel.Level2, 101);
        _candidatePreferred = CreateEmployee(2, "NV02", "Ưu Tiên", EmployeePosition.Sales, EmployeeLevel.Level2, 102);
        _candidateNormal = CreateEmployee(3, "NV03", "Thường", EmployeePosition.Sales, EmployeeLevel.Level1, 103);
        _candidateCashier = CreateEmployee(4, "NV04", "Thu Ngân", EmployeePosition.Cashier, EmployeeLevel.Level2, 104);
        _periodRepo.SchedulePeriods.Add(_period);
        _shiftTypeRepo.ShiftTypes.AddRange([_morningShift, _afternoonShift, _eveningShift]);
        _employeeRepo.Employees.AddRange([_leavingSales, _candidatePreferred, _candidateNormal, _candidateCashier]);
        _scheduleService = CreateService();
    }

    private WorkScheduleService CreateService(FakeClock? clock = null) => new(
        _workScheduleRepo, _periodRepo, _shiftTypeRepo, _employeeRepo, _availRepo, _leaveRepo, _validationService,
        new OrToolsSchedulingService(_validationService, NullLogger<OrToolsSchedulingService>.Instance),
        _notificationRepo, _auditLogRepo, _unitOfWork, clock ?? _clock, NullLogger<WorkScheduleService>.Instance);

    private static ShiftType CreateShift(int id, string code, string name, int startH, int endH, int order) =>
        new() { ShiftTypeId = id, ShiftCode = code, ShiftName = name, StartTime = new(startH, 0),
            EndTime = new(endH, 0), DurationHours = endH - startH, IsActive = true, DisplayOrder = order };

    private static Employee CreateEmployee(
        int id, string code, string name, EmployeePosition position, EmployeeLevel level, int accountId) =>
        new() { EmployeeId = id, EmployeeCode = code, FullName = name, Position = position,
            EmploymentType = EmploymentType.PartTime, Level = level, IsActive = true, UserAccountId = accountId };

    private ShiftAssignment Assign(
        int id, int empId, int shiftId, ShiftType shift, decimal? hours = null, string? reason = null) =>
        new() { ShiftAssignmentId = id, WorkScheduleId = 10, EmployeeId = empId, WorkDate = _workDate,
            ShiftTypeId = shiftId, Source = AssignmentSource.ORTools, ShiftType = shift,
            ActualWorkedHours = hours, WorkdayAdjustmentReason = reason };

    private WorkSchedule CreatePublishedSchedule(params ShiftAssignment[] assignments) =>
        AddAndReturn(_workScheduleRepo.Schedules, new WorkSchedule { WorkScheduleId = 10, SchedulePeriodId = 1,
            Status = ScheduleStatus.Published, ShiftAssignments = [.. assignments] });

    private LeaveRequest CreateApprovedLeave(int leaveId, int employeeId, int? shiftId, string reason = "Việc riêng") =>
        AddAndReturn(_leaveRepo.Requests, new LeaveRequest { LeaveRequestId = leaveId, EmployeeId = employeeId,
            SchedulePeriodId = 1, LeaveDate = _workDate, ShiftTypeId = shiftId, Status = LeaveRequestStatus.Approved,
            Employee = _employeeRepo.Employees.First(e => e.EmployeeId == employeeId), Reason = reason });

    private static T AddAndReturn<T>(ICollection<T> collection, T item) { collection.Add(item); return item; }

    private ShiftAssignment SetupStandard(int? candidateToMakeAvailable = null)
    {
        var target = Assign(100, 1, 1, _morningShift);
        CreatePublishedSchedule(target, Assign(105, 4, 1, _morningShift));
        CreateApprovedLeave(1, 1, 1);
        if (candidateToMakeAvailable.HasValue) AddAvail(candidateToMakeAvailable.Value);
        return target;
    }

    private void AddAvail(
        int empId, AvailabilityPreferenceLevel pref = AvailabilityPreferenceLevel.Preferred, int shiftId = 1) =>
        _availRepo.Availabilities.Add(new Availability { EmployeeId = empId, SchedulePeriodId = 1,
            WorkDate = _workDate, ShiftTypeId = shiftId, PreferenceLevel = pref });

    private Task<ServiceResult> ReplaceAsync(
        int candidateId, int leaveId = 1, int assignId = 100, int managerId = 999) =>
        _scheduleService.ReplaceApprovedLeaveAssignmentAsync(new LeaveReplacementRequestViewModel
        { LeaveRequestId = leaveId, ShiftAssignmentId = assignId, CandidateEmployeeId = candidateId }, managerId);

    [Fact(DisplayName = "TC-LEAVE-006 - GET gợi ý candidate: Preferred trước Normal, tie-break EmployeeCode")]
    public async Task TC_LEAVE_006_ApprovedLeave_PublishedSchedule_RanksPreferredBeforeNormal()
    {
        // Arrange
        _employeeRepo.Employees.Clear();
        _employeeRepo.Employees.AddRange([_leavingSales,
            CreateEmployee(5, "NV99", "Z", EmployeePosition.Sales, EmployeeLevel.Level2, 105),
            CreateEmployee(2, "NV02", "A", EmployeePosition.Sales, EmployeeLevel.Level2, 102),
            _candidateNormal, _candidateCashier]);
        CreatePublishedSchedule(Assign(100, 1, 1, _morningShift), Assign(105, 4, 1, _morningShift));
        CreateApprovedLeave(1, 1, 1);
        AddAvail(5);
        AddAvail(2);
        AddAvail(3, AvailabilityPreferenceLevel.Normal);
        // Act
        var result = await _scheduleService.GetLeaveReplacementAsync(1);
        // Assert
        Assert.NotNull(result);
        var assignmentVm = Assert.Single(result.AffectedAssignments);
        Assert.Equal(3, assignmentVm.Candidates.Count);
        Assert.Equal(["NV02", "NV99", "NV03"], assignmentVm.Candidates.Select(c => c.EmployeeCode));
        Assert.Equal([AvailabilityPreferenceLevel.Preferred, AvailabilityPreferenceLevel.Preferred,
            AvailabilityPreferenceLevel.Normal], assignmentVm.Candidates.Select(c => c.PreferenceLevel));
    }

    [Fact(DisplayName = "TC-LEAVE-007 - POST từ chối candidate có đơn nghỉ mới phát sinh")]
    public async Task TC_LEAVE_007_PostReplacement_RevalidatesLatestState_RejectsCandidateWithNewApprovedLeave()
    {
        // Arrange
        var target = SetupStandard(2);
        CreateApprovedLeave(2, 2, 1);
        // Act
        var result = await ReplaceAsync(2, managerId: 99);
        // Assert
        Assert.Equal((false, ErrorCodes.EmployeeOnLeave, 1), (result.Succeeded, result.ErrorCode, target.EmployeeId));
    }

    [Fact(DisplayName = "POST xếp người thay từ chối candidate khác Position với nhân viên nghỉ")]
    public async Task ReplaceApprovedLeaveAssignment_DifferentPosition_ReturnsInvalidShiftAssignment()
    {
        // Arrange
        SetupStandard(4);
        // Act
        var result = await ReplaceAsync(4, managerId: 99);
        // Assert
        Assert.Equal((false, ErrorCodes.InvalidShiftAssignment), (result.Succeeded, result.ErrorCode));
    }

    [Fact(DisplayName = "POST xếp người thay từ chối candidate chưa đăng ký rảnh cho ca")]
    public async Task ReplaceApprovedLeaveAssignment_CandidateNotAvailable_ReturnsEmployeeNotAvailable()
    {
        // Arrange
        SetupStandard();
        // Act
        var result = await ReplaceAsync(3, managerId: 99);
        // Assert
        Assert.Equal((false, ErrorCodes.EmployeeNotAvailable), (result.Succeeded, result.ErrorCode));
    }

    [Fact(DisplayName = "POST xếp người thay từ chối candidate nếu làm mất định mức Level 2+ của ca")]
    public async Task ReplaceApprovedLeaveAssignment_BreaksLevel2StaffingRequirement_ReturnsInvalidShiftAssignment()
    {
        // Arrange
        var target = Assign(100, 1, 1, _morningShift);
        _employeeRepo.Employees.Add(
            CreateEmployee(6, "NV06", "L1", EmployeePosition.Cashier, EmployeeLevel.Level1, 106));
        CreatePublishedSchedule(target, Assign(106, 6, 1, _morningShift));
        CreateApprovedLeave(1, 1, 1);
        AddAvail(3, AvailabilityPreferenceLevel.Normal);
        // Act
        var result = await ReplaceAsync(3, managerId: 99);
        // Assert
        Assert.Equal((false, ErrorCodes.InvalidShiftAssignment), (result.Succeeded, result.ErrorCode));
    }

    [Fact(DisplayName = "POST xếp người thay từ chối candidate nếu vi phạm giới hạn số ca trong ngày")]
    public async Task ReplaceApprovedLeaveAssignment_ViolatesDailyShiftLimit_ReturnsInvalidShiftAssignment()
    {
        // Arrange
        SetupStandard(2);
        _workScheduleRepo.Schedules.First().ShiftAssignments.Add(Assign(102, 2, 2, _afternoonShift));
        // Act
        var result = await ReplaceAsync(2, managerId: 99);
        // Assert
        Assert.Equal((false, ErrorCodes.InvalidShiftAssignment), (result.Succeeded, result.ErrorCode));
    }

    [Fact(DisplayName = "TC-LEAVE-008 - Thay thế thành công: cập nhật in-place, không SaveAssignmentsAsync")]
    public async Task TC_LEAVE_008_ManagerReplacement_Success_InPlaceMutation()
    {
        // Arrange
        var target = SetupStandard(2);
        var schedule = _workScheduleRepo.Schedules.First();
        // Act
        var result = await ReplaceAsync(2);
        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal((2, 100, AssignmentSource.Manual), (target.EmployeeId, target.ShiftAssignmentId, target.Source));
        Assert.Equal((ScheduleStatus.Published, 0), (schedule.Status, _workScheduleRepo.SaveAssignmentsCount));
    }

    [Fact(DisplayName = "Thay thế thành công tạo bản ghi AuditLog với Action REPLACE_APPROVED_LEAVE_ASSIGNMENT")]
    public async Task ReplaceApprovedLeaveAssignment_Success_CreatesAuditLog()
    {
        // Arrange
        SetupStandard(2);
        // Act
        var result = await ReplaceAsync(2, managerId: 999);
        // Assert
        Assert.True(result.Succeeded);
        var audit = Assert.Single(_auditLogRepo.Items, l => l.Action == "REPLACE_APPROVED_LEAVE_ASSIGNMENT");
        Assert.Equal(("100", 999), (audit.EntityId, audit.ActorAccountId));
    }

    [Fact(DisplayName = "Thay thế thành công gửi thông báo ScheduleChanged cho cả 2 nhân viên")]
    public async Task ReplaceApprovedLeaveAssignment_Success_CreatesScheduleChangedNotifications()
    {
        // Arrange
        SetupStandard(2);
        // Act
        var result = await ReplaceAsync(2);
        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(_notificationRepo.Items,
            n => n.ReceiverAccountId == 102 && n.Type == NotificationType.ScheduleChanged);
        Assert.Contains(_notificationRepo.Items,
            n => n.ReceiverAccountId == 101 && n.Type == NotificationType.ScheduleChanged);
    }

    [Fact(DisplayName = "Thay thế thành công giữ nguyên dữ liệu và điều chỉnh công của ca không liên quan")]
    public async Task ReplaceApprovedLeaveAssignment_Success_DoesNotModifyUnrelatedAssignment()
    {
        // Arrange
        SetupStandard(2);
        var other = Assign(200, 1, 2, _afternoonShift, 4.0m, "Ghi nhận công");
        _workScheduleRepo.Schedules.First().ShiftAssignments.Add(other);
        // Act
        var result = await ReplaceAsync(2);
        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal((1, 4.0m, "Ghi nhận công"),
            (other.EmployeeId, other.ActualWorkedHours, other.WorkdayAdjustmentReason));
    }

    [Theory(DisplayName = "Ca đã có điều chỉnh ngày công từ chối thay thế và không lưu thay đổi vào DB")]
    [InlineData(4.0, null)]
    [InlineData(null, "Điều chỉnh giờ công")]
    public async Task ReplaceApprovedLeaveAssignment_WithWorkdayAdjustment_ReturnsFailure(
        double? hours, string? reason)
    {
        // Arrange
        var target = SetupStandard(2);
        target.ActualWorkedHours = hours.HasValue ? (decimal)hours.Value : null;
        target.WorkdayAdjustmentReason = reason;
        // Act
        var result = await ReplaceAsync(2);
        // Assert
        Assert.Equal((false, 1, 0), (result.Succeeded, target.EmployeeId, _unitOfWork.SaveCount));
        Assert.Empty(_auditLogRepo.Items);
    }

    [Fact(DisplayName = "POST xếp người thay từ chối khi thiếu định mức nhân sự StaffingRequirement")]
    public async Task ReplaceApprovedLeaveAssignment_MissingStaffingRequirement_ReturnsInvalidShiftAssignment()
    {
        // Arrange
        var target = SetupStandard(2);
        foreach (var r in _period.StaffingRequirements) r.WorkDate = _workDate.AddDays(1);
        // Act
        var result = await ReplaceAsync(2);
        // Assert
        Assert.Equal((false, ErrorCodes.InvalidShiftAssignment, 1, 0),
            (result.Succeeded, result.ErrorCode, target.EmployeeId, _unitOfWork.SaveCount));
    }

    [Fact(DisplayName = "POST xếp người thay ca qua đêm trước giờ kết thúc thành công")]
    public async Task ReplaceApprovedLeaveAssignment_OvernightShiftBeforeEnd_AllowsReplacement()
    {
        // Arrange
        _eveningShift.StartTime = new(22, 0);
        _eveningShift.EndTime = new(6, 0);
        var target = Assign(100, 1, 3, _eveningShift);
        CreatePublishedSchedule(target, Assign(105, 4, 3, _eveningShift));
        CreateApprovedLeave(1, 1, 3);
        AddAvail(2, shiftId: 3);
        var service = CreateService(new FakeClock(new DateTime(2026, 8, 12, 19, 0, 0, DateTimeKind.Utc)));
        // Act
        var result = await service.ReplaceApprovedLeaveAssignmentAsync(
            new() { LeaveRequestId = 1, ShiftAssignmentId = 100, CandidateEmployeeId = 2 }, 999);
        // Assert
        Assert.Equal((true, 2), (result.Succeeded, target.EmployeeId));
    }

    [Fact(DisplayName = "TC-LEAVE-009 - Đơn nghỉ cả ngày: thay từng ca độc lập, không auto-assign")]
    public async Task TC_LEAVE_009_FullDayLeave_MultipleShifts_IndependentReplacement_NoAutoAssign()
    {
        // Arrange
        var morning = Assign(101, 1, 1, _morningShift);
        var afternoon = Assign(102, 1, 2, _afternoonShift);
        var schedule = CreatePublishedSchedule(morning, Assign(105, 4, 1, _morningShift), afternoon);
        CreateApprovedLeave(5, 1, null, "Nghỉ cả ngày");
        AddAvail(2);
        // Act
        var result = await ReplaceAsync(2, leaveId: 5, assignId: 101, managerId: 88);
        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal((2, 1), (morning.EmployeeId, afternoon.EmployeeId));
        Assert.DoesNotContain(schedule.ShiftAssignments, a => a.EmployeeId == 88);
    }

    [Fact(DisplayName = "GET đơn xin nghỉ cả ngày trả về đầy đủ các ca làm việc bị ảnh hưởng")]
    public async Task GetLeaveReplacement_FullDayLeave_ReturnsAllAffectedShifts()
    {
        // Arrange
        CreatePublishedSchedule(Assign(101, 1, 1, _morningShift), Assign(102, 1, 2, _afternoonShift));
        CreateApprovedLeave(5, 1, null, "Nghỉ cả ngày");
        // Act
        var result = await _scheduleService.GetLeaveReplacementAsync(5);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.AffectedAssignments.Count);
    }

    [Fact(DisplayName = "Ca làm việc đã kết thúc bị từ chối xếp người thay")]
    public async Task ReplaceApprovedLeaveAssignment_EndedShift_ReturnsLeaveRequestShiftEnded()
    {
        // Arrange
        var target = SetupStandard();
        target.WorkDate = new DateOnly(2026, 8, 9);
        _leaveRepo.Requests.First().LeaveDate = target.WorkDate;
        // Act
        var result = await ReplaceAsync(2);
        // Assert
        Assert.Equal((false, ErrorCodes.LeaveRequestShiftEnded), (result.Succeeded, result.ErrorCode));
    }

    [Fact(DisplayName = "Ca làm việc không tìm thấy ShiftType bị từ chối fail-safe với ShiftTypeNotFound")]
    public async Task ReplaceApprovedLeaveAssignment_MissingShiftType_ReturnsShiftTypeNotFound()
    {
        // Arrange
        var target = SetupStandard();
        target.ShiftTypeId = 999;
        target.ShiftType = null!;
        _leaveRepo.Requests.First().ShiftTypeId = 999;
        // Act
        var result = await ReplaceAsync(2);
        // Assert
        Assert.Equal((false, ErrorCodes.ShiftTypeNotFound), (result.Succeeded, result.ErrorCode));
    }

    [Fact(DisplayName = "POST xếp người thay chỉ dành cho Quản lý và có chống giả mạo yêu cầu")]
    public void WorkScheduleController_LeaveReplacementActions_HaveCorrectSecurityAttributes()
    {
        // Arrange
        var controller = typeof(WorkScheduleController);
        var method = controller.GetMethod(nameof(WorkScheduleController.ReplaceApprovedLeaveAssignment));
        // Act
        var auth = controller.GetCustomAttribute<AuthorizeAttribute>();
        var post = method?.GetCustomAttribute<HttpPostAttribute>();
        var csrf = method?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
        // Assert
        Assert.Equal(RoleNames.Manager, auth?.Roles);
        Assert.NotNull(post);
        Assert.NotNull(csrf);
    }
}
