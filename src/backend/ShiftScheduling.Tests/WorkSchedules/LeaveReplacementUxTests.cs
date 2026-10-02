using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.LeaveRequests.Controllers;
using ShiftScheduling.Features.LeaveRequests.Services;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Features.WorkSchedules.Controllers;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class LeaveReplacementUxTests
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
    private readonly LeaveRequestService _leaveService;
    private readonly SchedulePeriod _period;
    private readonly ShiftType _morningShift, _afternoonShift, _eveningShift;
    private readonly Employee _leavingEmployee, _candidateEmployee, _candidateEmployee2;
    private readonly DateOnly _workDate = new(2026, 8, 12);

    public LeaveReplacementUxTests()
    {
        _period = new SchedulePeriod
        {
            SchedulePeriodId = 1, PeriodName = "Kỳ lịch 1",
            WeekStartDate = new DateOnly(2026, 8, 10), WeekEndDate = new DateOnly(2026, 8, 16)
        };
        _morningShift = CreateShift(1, ShiftTypeConstants.MorningCode, "Sáng", 8, 12, 1);
        _afternoonShift = CreateShift(2, ShiftTypeConstants.AfternoonCode, "Chiều", 13, 17, 2);
        _eveningShift = CreateShift(3, ShiftTypeConstants.EveningCode, "Tối", 18, 22, 3);
        _leavingEmployee = CreateEmployee(1, 101, "NV01", "Nguyễn An", EmployeePosition.Sales);
        _candidateEmployee = CreateEmployee(2, 102, "NV02", "Trần Bình", EmployeePosition.Sales);
        _candidateEmployee2 = CreateEmployee(3, 103, "NV03", "Lê Cường", EmployeePosition.Sales);

        InitData();
        _scheduleService = new WorkScheduleService(
            _workScheduleRepo, _periodRepo, _shiftTypeRepo, _employeeRepo, _availRepo, _leaveRepo, _validationService,
            new OrToolsSchedulingService(_validationService, NullLogger<OrToolsSchedulingService>.Instance),
            _notificationRepo, _auditLogRepo, _unitOfWork, _clock, NullLogger<WorkScheduleService>.Instance);
        _leaveService = new LeaveRequestService(
            _leaveRepo, _notificationRepo, _auditLogRepo, _unitOfWork, _clock,
            NullLogger<LeaveRequestService>.Instance);
    }
    private void InitData()
    {
        _period.StaffingRequirements = new[] { _morningShift, _afternoonShift, _eveningShift }
            .Select(s => new StaffingRequirement
            {
                SchedulePeriodId = 1, WorkDate = _workDate, ShiftTypeId = s.ShiftTypeId,
                RequiredSalesStaff = 1, RequiredCashiers = 0, ShiftType = s
            }).ToList();
        _periodRepo.SchedulePeriods.Add(_period);
        _shiftTypeRepo.ShiftTypes.AddRange([_morningShift, _afternoonShift, _eveningShift]);
        _employeeRepo.Employees.AddRange([_leavingEmployee, _candidateEmployee, _candidateEmployee2]);
        _leaveRepo.Employees.AddRange([_leavingEmployee, _candidateEmployee, _candidateEmployee2]);
        _leaveRepo.Periods.Add(_period);
        _leaveRepo.ShiftTypes.AddRange([_morningShift, _afternoonShift, _eveningShift]);
        SeedCandidateAvailabilities();
    }
    private void SeedCandidateAvailabilities()
    {
        foreach (var empId in new[] { _candidateEmployee.EmployeeId, _candidateEmployee2.EmployeeId })
            foreach (var shiftId in new[] { 1, 2, 3 })
                AddAvail(empId, shiftId);
    }

    private void AddAvail(
        int empId, int shiftId, AvailabilityPreferenceLevel pref = AvailabilityPreferenceLevel.Normal) =>
        _availRepo.Availabilities.Add(new() { EmployeeId = empId, SchedulePeriodId = 1,
            WorkDate = _workDate, ShiftTypeId = shiftId, PreferenceLevel = pref });

    [Fact]
    public async Task Review_PendingLeaveWithPublishedAssignment_DisplaysCandidatePreview()
    {
        var (_, leave, _) = SetupPendingScenario();
        var controller = CreateLeaveRequestController();
        var result = await controller.Review(leave.LeaveRequestId, default);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LeaveRequestReviewViewModel>(view.Model);
        Assert.NotNull(model.ReplacementPreview);
        var assignment = Assert.Single(model.ReplacementPreview!.AffectedAssignments);
        Assert.Contains(assignment.Candidates, c => c.EmployeeId == _candidateEmployee.EmployeeId);
    }

    [Fact]
    public async Task CandidatePreview_RankedPreferredThenNormalThenEmployeeCode()
    {
        var (_, leave, _) = SetupPendingScenario();
        _availRepo.Availabilities.Clear();
        AddAvail(_candidateEmployee.EmployeeId, _morningShift.ShiftTypeId, AvailabilityPreferenceLevel.Normal);
        AddAvail(_candidateEmployee2.EmployeeId, _morningShift.ShiftTypeId, AvailabilityPreferenceLevel.Preferred);
        var preview = await _scheduleService.GetLeaveReplacementPreviewAsync(leave.LeaveRequestId);
        Assert.NotNull(preview);
        var candidates = preview!.AffectedAssignments[0].Candidates;
        Assert.Equal(_candidateEmployee2.EmployeeId, candidates[0].EmployeeId);
        Assert.Equal(_candidateEmployee.EmployeeId, candidates[1].EmployeeId);
    }

    [Fact]
    public async Task Preview_DoesNotMutateLeaveOrSchedule()
    {
        var (_, leave, assignment) = SetupPendingScenario();
        var preview = await _scheduleService.GetLeaveReplacementPreviewAsync(leave.LeaveRequestId);
        Assert.NotNull(preview);
        Assert.Equal(LeaveRequestStatus.Pending, leave.Status);
        Assert.Equal(_leavingEmployee.EmployeeId, assignment.EmployeeId);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Review_NavigateToReplacement_LeavesLeaveRequestPending()
    {
        var (_, leave, _) = SetupPendingScenario();
        var controller = CreateWorkScheduleController();
        var result = await controller.LeaveReplacementPreview(leave.LeaveRequestId, default);
        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<LeaveReplacementViewModel>(view.Model);
        Assert.Equal(LeaveRequestStatus.Pending, leave.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ApproveLeaveWithReplacement_ValidCandidate_AtomicallyChangesAssignmentAndApprovesLeave()
    {
        var (_, leave, assignment) = SetupPendingScenario();
        var model = CreateApproveModel(leave, (assignment.ShiftAssignmentId, _candidateEmployee.EmployeeId));
        var result = await _scheduleService.ApproveLeaveWithReplacementAsync(model, 99);
        Assert.True(result.Succeeded);
        Assert.Equal(_candidateEmployee.EmployeeId, assignment.EmployeeId);
        Assert.Equal(AssignmentSource.Manual, assignment.Source);
        Assert.Equal(LeaveRequestStatus.Approved, leave.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(99999, null, null)]
    [InlineData(2, "invalid-base64-format!!", ErrorCodes.ConcurrencyConflict)]
    public async Task ApproveLeaveWithReplacement_InvalidInput_LeavesBothUnchanged(
        int candidateId, string? rowVersion, string? expectedError)
    {
        var (_, leave, assignment) = SetupPendingScenario();
        var model = CreateApproveModel(leave, (assignment.ShiftAssignmentId, candidateId));
        if (rowVersion != null) model.RowVersion = rowVersion;

        var result = await _scheduleService.ApproveLeaveWithReplacementAsync(model, 99);

        Assert.False(result.Succeeded);
        if (expectedError != null) Assert.Equal(expectedError, result.ErrorCode);
        AssertUnchanged(leave, assignment);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApproveLeaveWithReplacement_FullDayInvalid_LeavesAllUnchanged(bool isOneCandidateInvalid)
    {
        var (_, leave, a1, a2) = SetupFullDayPendingScenario();
        var model = isOneCandidateInvalid
            ? CreateApproveModel(leave,
                (a1.ShiftAssignmentId, _candidateEmployee.EmployeeId),
                (a2.ShiftAssignmentId, 99999))
            : CreateApproveModel(leave, (a1.ShiftAssignmentId, _candidateEmployee.EmployeeId));

        var result = await _scheduleService.ApproveLeaveWithReplacementAsync(model, 99);

        Assert.False(result.Succeeded);
        AssertUnchanged(leave, a1, a2);
    }

    [Fact]
    public async Task ApproveLeaveWithReplacement_FullDayAllValid_AllAssignmentsChangedAndApprovedWithSingleSave()
    {
        var (_, leave, a1, a2) = SetupFullDayPendingScenario();
        var model = CreateApproveModel(leave,
            (a1.ShiftAssignmentId, _candidateEmployee.EmployeeId),
            (a2.ShiftAssignmentId, _candidateEmployee2.EmployeeId));
        var result = await _scheduleService.ApproveLeaveWithReplacementAsync(model, 99);

        Assert.True(result.Succeeded);
        Assert.Equal(_candidateEmployee.EmployeeId, a1.EmployeeId);
        Assert.Equal(_candidateEmployee2.EmployeeId, a2.EmployeeId);
        Assert.Equal(LeaveRequestStatus.Approved, leave.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Review_NoCandidateAvailable_CannotFollowApprovePath_RejectRemainsAvailable()
    {
        var (_, leave, _) = SetupPendingScenario();
        _employeeRepo.Employees.Clear();
        _employeeRepo.Employees.Add(_leavingEmployee);

        var preview = await _scheduleService.GetLeaveReplacementPreviewAsync(leave.LeaveRequestId);
        Assert.NotNull(preview);
        Assert.Empty(preview!.AffectedAssignments[0].Candidates);

        var reject = await _leaveService.ReviewAsync(
            leave.LeaveRequestId, false, "Không có nhân sự", 99,
            Convert.ToBase64String(leave.RowVersion));
        Assert.True(reject.Succeeded);
        Assert.Equal(LeaveRequestStatus.Rejected, leave.Status);
    }

    [Fact]
    public async Task ApproveLeaveWithReplacement_SuccessMessage_IncludesReplacementEmployeeIdentity()
    {
        var (_, leave, assignment) = SetupPendingScenario();
        var model = CreateApproveModel(leave, (assignment.ShiftAssignmentId, _candidateEmployee.EmployeeId));
        var result = await _scheduleService.ApproveLeaveWithReplacementAsync(model, 99);

        Assert.True(result.Succeeded);
        Assert.Contains(_leavingEmployee.FullName, result.SuccessMessage);
        Assert.Contains(_leavingEmployee.EmployeeCode, result.SuccessMessage);
        Assert.Contains(_candidateEmployee.FullName, result.SuccessMessage);
        Assert.Contains(_candidateEmployee.EmployeeCode, result.SuccessMessage);
    }

    [Fact]
    public async Task LeaveReplacement_ResolvedState_AndStaleError_AreMutuallyExclusive()
    {
        var (_, leave, assignment) = SetupPendingScenario();
        assignment.EmployeeId = _candidateEmployee.EmployeeId;
        var controller = CreateWorkScheduleController();
        controller.TempData["Error"] = "Lỗi cũ";

        var result = await controller.LeaveReplacementPreview(leave.LeaveRequestId, default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LeaveReplacementViewModel>(view.Model);
        Assert.Empty(model.AffectedAssignments);
        Assert.False(controller.TempData.ContainsKey("Error"));
    }

    [Fact]
    public async Task ApproveEmergencyLeave_WhenCandidatesAvailable_RejectsEmergencyApproval()
    {
        var (_, leave, _) = SetupPendingScenario();
        var result = await _scheduleService.ApproveEmergencyLeaveAsync(CreateEmergencyModel(leave), 99);

        Assert.False(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Pending, leave.Status);
    }

    [Fact]
    public async Task ApproveEmergencyLeave_WhenNoCandidateAvailable_SucceedsAndPreservesAssignment()
    {
        var (_, leave, assignment) = SetupPendingScenario();
        _availRepo.Availabilities.Clear();
        var result = await _scheduleService.ApproveEmergencyLeaveAsync(CreateEmergencyModel(leave), 99);

        Assert.True(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Approved, leave.Status);
        Assert.Equal(_leavingEmployee.EmployeeId, assignment.EmployeeId);
    }

    [Fact]
    public async Task ApproveEmergencyLeave_InvalidRowVersion_ReturnsConcurrencyConflict()
    {
        var (_, leave, assignment) = SetupPendingScenario();
        _availRepo.Availabilities.Clear();
        var model = CreateEmergencyModel(leave, "InvalidBase64!!!");
        var result = await _scheduleService.ApproveEmergencyLeaveAsync(model, 99);

        Assert.False(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Pending, leave.Status);
        Assert.Equal(_leavingEmployee.EmployeeId, assignment.EmployeeId);
    }

    private static ApproveEmergencyLeaveRequestViewModel CreateEmergencyModel(LeaveRequest leave, string? rv = null) =>
        new() { LeaveRequestId = leave.LeaveRequestId, RowVersion = rv ?? Convert.ToBase64String(leave.RowVersion) };

    private static ApproveLeaveWithReplacementRequestViewModel CreateApproveModel(
        LeaveRequest leave, params (int AssignmentId, int CandidateId)[] replacements) => new()
    {
        LeaveRequestId = leave.LeaveRequestId, RowVersion = Convert.ToBase64String(leave.RowVersion),
        Replacements = replacements.Select(r => new ShiftReplacementItemViewModel
            { ShiftAssignmentId = r.AssignmentId, CandidateEmployeeId = r.CandidateId }).ToList()
    };

    private void AssertUnchanged(LeaveRequest leave, params ShiftAssignment[] assignments)
    {
        Assert.Equal(LeaveRequestStatus.Pending, leave.Status);
        Assert.All(assignments, a => Assert.Equal(_leavingEmployee.EmployeeId, a.EmployeeId));
    }

    private (WorkSchedule Schedule, LeaveRequest Leave, ShiftAssignment Assignment) SetupPendingScenario()
    {
        var schedule = SetupPublishedSchedule();
        var a = AddAssignment(schedule, _leavingEmployee.EmployeeId, _morningShift.ShiftTypeId);
        _leaveRepo.PublishedAssignments.Add(a);
        var leave = AddLeaveRequest(
            10, _leavingEmployee.EmployeeId, _morningShift.ShiftTypeId, LeaveRequestStatus.Pending);
        return (schedule, leave, a);
    }

    private (WorkSchedule Schedule, LeaveRequest Leave, ShiftAssignment A1, ShiftAssignment A2)
        SetupFullDayPendingScenario()
    {
        var schedule = SetupPublishedSchedule();
        var a1 = AddAssignment(schedule, _leavingEmployee.EmployeeId, _morningShift.ShiftTypeId);
        var a2 = AddAssignment(schedule, _leavingEmployee.EmployeeId, _afternoonShift.ShiftTypeId);
        _leaveRepo.PublishedAssignments.AddRange([a1, a2]);
        var leave = AddLeaveRequest(11, _leavingEmployee.EmployeeId, null, LeaveRequestStatus.Pending);
        return (schedule, leave, a1, a2);
    }

    private WorkSchedule SetupPublishedSchedule()
    {
        var schedule = new WorkSchedule
        {
            WorkScheduleId = 1, SchedulePeriodId = _period.SchedulePeriodId, SchedulePeriod = _period,
            Status = ScheduleStatus.Published, PublishedAt = _clock.UtcNow, PublishedByAccountId = 99
        };
        _workScheduleRepo.Schedules.Add(schedule);
        return schedule;
    }

    private ShiftAssignment AddAssignment(WorkSchedule schedule, int employeeId, int shiftTypeId)
    {
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = schedule.ShiftAssignments.Count + 1, WorkScheduleId = schedule.WorkScheduleId,
            WorkSchedule = schedule, EmployeeId = employeeId, WorkDate = _workDate, ShiftTypeId = shiftTypeId
        };
        schedule.ShiftAssignments.Add(assignment);
        return assignment;
    }

    private LeaveRequest AddLeaveRequest(int id, int employeeId, int? shiftTypeId, LeaveRequestStatus status)
    {
        var leave = new LeaveRequest
        {
            LeaveRequestId = id, SchedulePeriodId = _period.SchedulePeriodId, SchedulePeriod = _period,
            EmployeeId = employeeId, LeaveDate = _workDate, ShiftTypeId = shiftTypeId, Status = status,
            RowVersion = [1, 2, 3, 4]
        };
        _leaveRepo.Requests.Add(leave);
        _leaveRepo.WireLeaveRequest(leave);
        return leave;
    }
    private WorkScheduleController CreateWorkScheduleController(int accountId = 99) =>
        new(_scheduleService) { ControllerContext = new() { HttpContext = CreateHttpContext(accountId) },
            TempData = new TempDataDictionary(CreateHttpContext(accountId), new TestTempDataProvider()) };
    private LeaveRequestController CreateLeaveRequestController(int accountId = 99) =>
        new(_leaveService, _scheduleService) { ControllerContext = new() { HttpContext = CreateHttpContext(accountId) },
            TempData = new TempDataDictionary(CreateHttpContext(accountId), new TestTempDataProvider()) };
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

    private static ShiftType CreateShift(int id, string code, string name, int startH, int endH, int order) =>
        new() { ShiftTypeId = id, ShiftCode = code, ShiftName = name, StartTime = new TimeOnly(startH, 0),
            EndTime = new TimeOnly(endH, 0), DurationHours = endH - startH, DisplayOrder = order, IsActive = true };

    private static Employee CreateEmployee(int id, int accountId, string code, string name, EmployeePosition pos) =>
        new()
        {
            EmployeeId = id, UserAccountId = accountId, EmployeeCode = code, FullName = name, Position = pos,
            EmploymentType = EmploymentType.PartTime, Level = EmployeeLevel.Level2, IsActive = true,
            UserAccount = new()
                { UserAccountId = accountId, Username = code.ToLowerInvariant(), Role = UserRole.Employee }
        };

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
