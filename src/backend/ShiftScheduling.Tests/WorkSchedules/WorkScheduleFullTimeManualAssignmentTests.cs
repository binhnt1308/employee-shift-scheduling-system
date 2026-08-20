using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class WorkScheduleFullTimeManualAssignmentTests
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
    private readonly FakeClock _clock = new();
    private readonly SchedulingValidationService _validationService = new();
    private readonly OrToolsSchedulingService _orToolsService;
    private readonly WorkScheduleService _scheduleService;

    public WorkScheduleFullTimeManualAssignmentTests()
    {
        _orToolsService = new OrToolsSchedulingService(
            _validationService,
            NullLogger<OrToolsSchedulingService>.Instance);

        _scheduleService = new WorkScheduleService(
            _workScheduleRepo,
            _periodRepo,
            _shiftTypeRepo,
            _employeeRepo,
            _availRepo,
            _leaveRepo,
            _validationService,
            _orToolsService,
            _notificationRepo,
            _auditLogRepo,
            _unitOfWork,
            _clock,
            NullLogger<WorkScheduleService>.Instance);
    }

    [Fact(DisplayName = "TC-FT-01 - FT 0 shift, assign Morning -> SUCCESS as intermediate state")]
    public async Task AssignEmployeeManualAsync_FullTimeZeroShifts_AssignMorning_Succeeds()
    {
        var (period, emp, morning, _, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        var model = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        };

        var result = await _scheduleService.AssignEmployeeManualAsync(model, accountId: 2);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-FT-02 - FT Morning, assign Afternoon -> SUCCESS final pair Morning+Afternoon")]
    public async Task AssignEmployeeManualAsync_FullTimeMorning_AssignAfternoon_Succeeds()
    {
        var (period, emp, morning, afternoon, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        // Step 1: Assign Morning
        await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);

        // Step 2: Assign Afternoon
        var result = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = afternoon.ShiftTypeId
        }, accountId: 2);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-FT-03 - FT 0 shift, assign Afternoon then Evening -> SUCCESS (Afternoon+Evening)")]
    public async Task AssignEmployeeManualAsync_FullTime_AssignAfternoonThenEvening_Succeeds()
    {
        var (period, emp, _, afternoon, evening) = await SetupPeriodWithFullTimeEmployeeAsync();

        await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = afternoon.ShiftTypeId
        }, accountId: 2);

        var result = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = evening.ShiftTypeId
        }, accountId: 2);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-FT-04 - FT 0 shift, assign Evening then Afternoon -> SUCCESS (Evening+Afternoon)")]
    public async Task AssignEmployeeManualAsync_FullTime_AssignEveningThenAfternoon_Succeeds()
    {
        var (period, emp, _, afternoon, evening) = await SetupPeriodWithFullTimeEmployeeAsync();

        await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = evening.ShiftTypeId
        }, accountId: 2);

        var result = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = afternoon.ShiftTypeId
        }, accountId: 2);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-FT-05 - FT Morning, assign Evening -> REJECT non-consecutive pair")]
    public async Task AssignEmployeeManualAsync_FullTimeMorning_AssignEvening_ReturnsFailure()
    {
        var (period, emp, morning, _, evening) = await SetupPeriodWithFullTimeEmployeeAsync();

        await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);

        var result = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = evening.ShiftTypeId
        }, accountId: 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
        Assert.Contains("2 ca liên tiếp", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-FT-06 - FT only Morning -> Final/Publish validation returns INVALID")]
    public async Task PublishScheduleAsync_FullTimeOnlyOneShift_BlocksPublishing()
    {
        var (period, emp, morning, _, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);

        var publishResult = await _scheduleService.PublishScheduleAsync(period.SchedulePeriodId, accountId: 2);

        Assert.False(publishResult.Succeeded);
        Assert.Contains("vi phạm bắt buộc", publishResult.ErrorMessage);
    }

    [Fact(DisplayName = "TC-FT-07 - FT Morning+Afternoon -> Validation PASSES FT consecutive rule")]
    public void ValidateSchedule_FullTimeMorningAndAfternoon_PassesFTConsecutiveRule()
    {
        var period = CreateStandardPeriod();
        var shifts = CreateThreeFixedShifts();
        var emp = CreateEmployee(1, EmploymentType.FullTime);

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = emp.EmployeeId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[0].ShiftTypeId },
            new ShiftAssignment { EmployeeId = emp.EmployeeId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[1].ShiftTypeId }
        };

        var avails = new List<Availability>
        {
            new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[0].ShiftTypeId },
            new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[1].ShiftTypeId }
        };

        var result = _validationService.ValidateSchedule(
            period, new[] { emp }, avails, new List<LeaveRequest>(), new List<StaffingRequirement>(), assignments, shifts);

        Assert.DoesNotContain(result.HardErrors, e => e.Contains("không đạt tổ hợp 2 ca liên tiếp"));
    }

    [Fact(DisplayName = "TC-FT-08 - FT Afternoon+Evening -> Validation PASSES FT consecutive rule")]
    public void ValidateSchedule_FullTimeAfternoonAndEvening_PassesFTConsecutiveRule()
    {
        var period = CreateStandardPeriod();
        var shifts = CreateThreeFixedShifts();
        var emp = CreateEmployee(1, EmploymentType.FullTime);

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = emp.EmployeeId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[1].ShiftTypeId },
            new ShiftAssignment { EmployeeId = emp.EmployeeId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[2].ShiftTypeId }
        };

        var avails = new List<Availability>
        {
            new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[1].ShiftTypeId },
            new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = shifts[2].ShiftTypeId }
        };

        var result = _validationService.ValidateSchedule(
            period, new[] { emp }, avails, new List<LeaveRequest>(), new List<StaffingRequirement>(), assignments, shifts);

        Assert.DoesNotContain(result.HardErrors, e => e.Contains("không đạt tổ hợp 2 ca liên tiếp"));
    }

    [Fact(DisplayName = "TC-PT-01 - PT manual assignment behavior unchanged (2 shifts in 1 day rejected)")]
    public async Task AssignEmployeeManualAsync_PartTimeSecondShiftInSameDay_ReturnsFailure()
    {
        var period = CreateStandardPeriod();
        _periodRepo.SchedulePeriods.Add(period);

        var emp = CreateEmployee(1, EmploymentType.PartTime);
        _employeeRepo.Employees.Add(emp);

        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12);
        var afternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều", 12, 16);
        _shiftTypeRepo.ShiftTypes.AddRange(new[] { morning, afternoon });

        await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);

        _availRepo.Availabilities.Add(new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = morning.ShiftTypeId });
        _availRepo.Availabilities.Add(new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = afternoon.ShiftTypeId });

        // First shift
        await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);

        // Second shift on same day
        var result = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = afternoon.ShiftTypeId
        }, accountId: 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
        Assert.Contains("tối đa 1 ca/ngày", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-DISPLAY-01 - GetEligibleEmployeesForShiftAsync returns clear FT/PT label")]
    public async Task GetEligibleEmployeesForShiftAsync_ReturnsEmploymentTypeLabel()
    {
        var (period, empFt, morning, _, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        var options = await _scheduleService.GetEligibleEmployeesForShiftAsync(
            period.SchedulePeriodId, period.WeekStartDate, morning.ShiftTypeId);

        var option = options.FirstOrDefault(o => o.EmployeeId == empFt.EmployeeId);
        Assert.NotNull(option);
        Assert.Equal("FT", option!.EmploymentTypeLabel);
        Assert.True(option.IsEligible);
    }

    private async Task<(SchedulePeriod Period, Employee Emp, ShiftType Morning, ShiftType Afternoon, ShiftType Evening)> SetupPeriodWithFullTimeEmployeeAsync()
    {
        var period = CreateStandardPeriod();
        _periodRepo.SchedulePeriods.Add(period);

        var emp = CreateEmployee(1, EmploymentType.FullTime);
        _employeeRepo.Employees.Add(emp);

        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12);
        var afternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều", 12, 16);
        var evening = CreateShiftType(3, "EVENING", "Ca Tối", 16, 20);
        _shiftTypeRepo.ShiftTypes.AddRange(new[] { morning, afternoon, evening });

        await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);

        // Add availability for all shifts
        _availRepo.Availabilities.Add(new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = morning.ShiftTypeId });
        _availRepo.Availabilities.Add(new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = afternoon.ShiftTypeId });
        _availRepo.Availabilities.Add(new Availability { EmployeeId = emp.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = evening.ShiftTypeId });

        return (period, emp, morning, afternoon, evening);
    }

    private static SchedulePeriod CreateStandardPeriod() => new()
    {
        SchedulePeriodId = 1,
        WeekStartDate = new DateOnly(2026, 8, 10),
        WeekEndDate = new DateOnly(2026, 8, 16)
    };

    private static Employee CreateEmployee(int id, EmploymentType type) => new()
    {
        EmployeeId = id,
        EmployeeCode = $"NV{id:D2}",
        FullName = $"Fullname {id}",
        EmploymentType = type,
        Position = EmployeePosition.Sales,
        Level = EmployeeLevel.Level2,
        IsActive = true
    };

    private static ShiftType CreateShiftType(int id, string code, string name, int start, int end) => new()
    {
        ShiftTypeId = id,
        ShiftCode = code,
        ShiftName = name,
        StartTime = new TimeOnly(start, 0),
        EndTime = new TimeOnly(end, 0),
        IsActive = true
    };

    private static List<ShiftType> CreateThreeFixedShifts() => new()
    {
        CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12),
        CreateShiftType(2, "AFTERNOON", "Ca Chiều", 12, 16),
        CreateShiftType(3, "EVENING", "Ca Tối", 16, 20)
    };
}
