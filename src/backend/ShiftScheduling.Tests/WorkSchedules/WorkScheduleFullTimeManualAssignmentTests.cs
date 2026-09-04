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

    [Fact(DisplayName = "TC-FT-06 - FT only Morning -> 1 shift is valid under BR-01, manual assign succeeds and validation passes")]
    public async Task ManualAssignAndValidate_FullTimeOnlyOneShift_IsValidUnderBR01()
    {
        var (period, emp, morning, afternoon, evening) = await SetupPeriodWithFullTimeEmployeeAsync();

        var assignResult = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);

        Assert.True(assignResult.Succeeded);

        // Validation với staffing requirement có đầy đủ 3 ca của kỳ
        var staffingReqs = new List<StaffingRequirement>
        {
            new()
            {
                SchedulePeriodId = period.SchedulePeriodId,
                WorkDate = period.WeekStartDate,
                ShiftTypeId = morning.ShiftTypeId,
                ShiftType = morning,
                RequiredSalesStaff = 1,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 1,
                MinimumLevel = EmployeeLevel.Level2
            },
            new()
            {
                SchedulePeriodId = period.SchedulePeriodId,
                WorkDate = period.WeekStartDate,
                ShiftTypeId = afternoon.ShiftTypeId,
                ShiftType = afternoon,
                RequiredSalesStaff = 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 0,
                MinimumLevel = EmployeeLevel.Level2
            },
            new()
            {
                SchedulePeriodId = period.SchedulePeriodId,
                WorkDate = period.WeekStartDate,
                ShiftTypeId = evening.ShiftTypeId,
                ShiftType = evening,
                RequiredSalesStaff = 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 0,
                MinimumLevel = EmployeeLevel.Level2
            }
        };

        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = emp.EmployeeId, WorkDate = period.WeekStartDate, ShiftTypeId = morning.ShiftTypeId }
        };

        var valResult = _validationService.ValidateSchedule(
            period, new[] { emp },
            new[] { new Availability { EmployeeId = emp.EmployeeId, WorkDate = period.WeekStartDate, ShiftTypeId = morning.ShiftTypeId } },
            Array.Empty<LeaveRequest>(), staffingReqs, assignments, new[] { morning, afternoon, evening });

        Assert.True(valResult.IsValid);
        Assert.DoesNotContain(valResult.HardErrors, e => e.Contains("2 ca liên tiếp") || e.Contains("tối đa"));
    }

    [Fact(DisplayName = "PublishScheduleAsync_WhenStaffingRequirementsMissing_FailsAndRemainsDraft")]
    public async Task PublishScheduleAsync_WhenStaffingRequirementsMissing_FailsAndRemainsDraft()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 999,
            PeriodName = "Kỳ không định mức",
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7)
        };
        _periodRepo.SchedulePeriods.Add(period);
        await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);

        var publishResult = await _scheduleService.PublishScheduleAsync(period.SchedulePeriodId, accountId: 2);

        Assert.False(publishResult.Succeeded);
        var schedule = await _workScheduleRepo.GetByPeriodIdAsync(period.SchedulePeriodId, false, false);
        Assert.NotNull(schedule);
        Assert.Equal(ScheduleStatus.Draft, schedule.Status);
        Assert.DoesNotContain(_notificationRepo.Items, n => n.Title.Contains("công bố") || n.Title.Contains("Công bố"));
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
            period, new[] { emp }, avails, new List<LeaveRequest>(), period.StaffingRequirements.ToList(), assignments, shifts);

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
            period, new[] { emp }, avails, new List<LeaveRequest>(), period.StaffingRequirements.ToList(), assignments, shifts);

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

    [Fact(DisplayName = "TC-FT-09 - FT đã có Sáng+Chiều, phân thêm ca Tối cùng ngày -> REJECT tối đa 2 ca/ngày")]
    public async Task AssignEmployeeManualAsync_FullTimeThirdShiftInSameDay_ReturnsFailure()
    {
        var (period, emp, morning, afternoon, evening) = await SetupPeriodWithFullTimeEmployeeAsync();

        var draft = await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);
        draft.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 1,
            WorkScheduleId = draft.WorkScheduleId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId,
            Source = AssignmentSource.Manual,
            WorkSchedule = draft
        });
        draft.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 2,
            WorkScheduleId = draft.WorkScheduleId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = afternoon.ShiftTypeId,
            Source = AssignmentSource.Manual,
            WorkSchedule = draft
        });

        Assert.Equal(2, draft.ShiftAssignments.Count);

        var result = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = evening.ShiftTypeId
        }, accountId: 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
        Assert.Contains("tối đa 2 ca/ngày", result.ErrorMessage);
        Assert.Equal(2, draft.ShiftAssignments.Count);
        Assert.DoesNotContain(draft.ShiftAssignments, a => a.ShiftTypeId == evening.ShiftTypeId);
    }

    [Fact(DisplayName = "TC-DISPLAY-01 - GetEligibleEmployeesForShiftAsync returns clear FT/PT and Position labels")]
    public async Task GetEligibleEmployeesForShiftAsync_ReturnsEmploymentTypeAndPositionLabels()
    {
        var (period, empFt, morning, _, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        var options = await _scheduleService.GetEligibleEmployeesForShiftAsync(
            period.SchedulePeriodId, period.WeekStartDate, morning.ShiftTypeId);

        var option = options.FirstOrDefault(o => o.EmployeeId == empFt.EmployeeId);
        Assert.NotNull(option);
        Assert.Equal("FT", option!.EmploymentTypeLabel);
        Assert.Equal("Tư vấn", option.PositionLabel);
        Assert.True(option.IsEligible);
    }

    [Fact(DisplayName = "TC-BR01-FT-01 - Khi cấu hình Max=1, FT phân ca thứ hai trong ngày bị từ chối")]
    public async Task AssignEmployeeManualAsync_WhenPolicyMax1_FullTimeSecondShift_ReturnsFailure()
    {
        var (period, emp, morning, afternoon, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        // Cấu hình kỳ với MaxShiftsPerEmployeePerDay = 1 (Version 2)
        period.StaffingRequirements.Clear();
        var shiftsV2 = new[]
        {
            CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12, maxShifts: 1, allowSplit: false, version: 2),
            CreateShiftType(2, "AFTERNOON", "Ca Chiều", 12, 16, maxShifts: 1, allowSplit: false, version: 2),
            CreateShiftType(3, "EVENING", "Ca Tối", 16, 20, maxShifts: 1, allowSplit: false, version: 2)
        };
        _shiftTypeRepo.ShiftTypes.Clear();
        _shiftTypeRepo.ShiftTypes.AddRange(shiftsV2);
        foreach (var s in shiftsV2)
        {
            period.StaffingRequirements.Add(new StaffingRequirement
            {
                SchedulePeriodId = period.SchedulePeriodId,
                WorkDate = period.WeekStartDate,
                ShiftTypeId = s.ShiftTypeId,
                ShiftType = s,
                RequiredSalesStaff = 1
            });
        }

        // Phân ca 1 (Sáng)
        var res1 = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);
        Assert.True(res1.Succeeded);

        // Phân ca 2 (Chiều) cùng ngày -> bị chặn do Max=1
        var res2 = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = afternoon.ShiftTypeId
        }, accountId: 2);

        Assert.False(res2.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, res2.ErrorCode);
        Assert.Contains("tối đa 1 ca/ngày", res2.ErrorMessage);
    }

    [Fact(DisplayName = "TC-BR01-FT-02 - Khi cấu hình AllowSplitShift=true, FT phân Sáng-Tối hợp lệ")]
    public async Task AssignEmployeeManualAsync_WhenPolicyAllowsSplitShift_FullTimeMorningAndEvening_Succeeds()
    {
        var (period, emp, morning, _, evening) = await SetupPeriodWithFullTimeEmployeeAsync();

        // Cấu hình kỳ với Max=2, AllowSplitShift=true (Version 2)
        period.StaffingRequirements.Clear();
        var shiftsSplit = new[]
        {
            CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12, maxShifts: 2, allowSplit: true, version: 2),
            CreateShiftType(2, "AFTERNOON", "Ca Chiều", 12, 16, maxShifts: 2, allowSplit: true, version: 2),
            CreateShiftType(3, "EVENING", "Ca Tối", 16, 20, maxShifts: 2, allowSplit: true, version: 2)
        };
        _shiftTypeRepo.ShiftTypes.Clear();
        _shiftTypeRepo.ShiftTypes.AddRange(shiftsSplit);
        foreach (var s in shiftsSplit)
        {
            period.StaffingRequirements.Add(new StaffingRequirement
            {
                SchedulePeriodId = period.SchedulePeriodId,
                WorkDate = period.WeekStartDate,
                ShiftTypeId = s.ShiftTypeId,
                ShiftType = s,
                RequiredSalesStaff = 1
            });
        }

        // Phân ca 1 (Sáng)
        var res1 = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);
        Assert.True(res1.Succeeded);

        // Phân ca 2 (Tối) cùng ngày -> thành công vì AllowSplitShift=true
        var res2 = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = evening.ShiftTypeId
        }, accountId: 2);

        Assert.True(res2.Succeeded);
    }

    [Fact(DisplayName = "TC-BR01-FT-03 - Hai ca chồng lấn thời gian trong ngày luôn bị chặn")]
    public async Task AssignEmployeeManualAsync_WhenShiftsOverlapInTime_ReturnsFailure()
    {
        var (period, emp, morning, _, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        // Tạo ca chồng giờ với ca sáng (8:00 - 12:00): ca 4 (10:00 - 14:00)
        var overlapShift = new ShiftType
        {
            ShiftTypeId = 4,
            ShiftCode = "OVERLAP",
            ShiftName = "Ca Chồng Giờ",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(14, 0),
            DurationHours = 4,
            DisplayOrder = 4,
            IsActive = true,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = true
        };
        _shiftTypeRepo.ShiftTypes.Add(overlapShift);
        _availRepo.Availabilities.Add(new Availability
        {
            EmployeeId = emp.EmployeeId,
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = overlapShift.ShiftTypeId
        });

        // Phân ca 1 (Sáng: 8h-12h)
        var res1 = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        }, accountId: 2);
        Assert.True(res1.Succeeded);

        // Phân ca 2 (10h-14h) -> chồng giờ với ca 1
        var res2 = await _scheduleService.AssignEmployeeManualAsync(new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = overlapShift.ShiftTypeId
        }, accountId: 2);

        Assert.False(res2.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, res2.ErrorCode);
        Assert.Contains("chồng thời gian", res2.ErrorMessage);
    }

    [Fact(DisplayName = "TC-MANUAL-REQ-01 - GetEligibleEmployeesForShiftAsync nạp requirements khi includeRequirements = true")]
    public async Task GetEligibleEmployeesForShiftAsync_WhenRequirementsRequireExplicitInclude_LoadsPeriodPolicy()
    {
        // 1. Tạo period, employee, ba ShiftType, Availability bằng helper hiện có
        var (period, emp, morning, _, _) = await SetupPeriodWithFullTimeEmployeeAsync();

        // 2. Lưu bản sao các StaffingRequirements của period
        var requirements = period.StaffingRequirements.ToList();

        // 3. Đưa các requirement đó vào FakeSchedulePeriodRepository.Requirements
        _periodRepo.Requirements.AddRange(requirements);

        // 4. Xóa period.StaffingRequirements để mô phỏng navigation chưa được EF load
        period.StaffingRequirements = new List<StaffingRequirement>();

        // 5. Gọi GetEligibleEmployeesForShiftAsync
        var options = await _scheduleService.GetEligibleEmployeesForShiftAsync(
            period.SchedulePeriodId, period.WeekStartDate, morning.ShiftTypeId);

        // 6 & 7. Assertion
        var option = Assert.Single(options, o => o.EmployeeId == emp.EmployeeId);
        Assert.True(option.IsEligible, option.IneligibilityReason);
        Assert.DoesNotContain("Kỳ lịch không có dữ liệu ca làm việc hoặc định mức nhân sự.", option.IneligibilityReason ?? string.Empty);
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

    private static SchedulePeriod CreateStandardPeriod()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };
        var shifts = CreateThreeFixedShifts();
        for (int d = 0; d < 7; d++)
        {
            foreach (var s in shifts)
            {
                period.StaffingRequirements.Add(new StaffingRequirement
                {
                    SchedulePeriodId = 1,
                    WorkDate = period.WeekStartDate.AddDays(d),
                    ShiftTypeId = s.ShiftTypeId,
                    ShiftType = s,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 0,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }
        return period;
    }

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

    private static ShiftType CreateShiftType(int id, string code, string name, int start, int end, int maxShifts = 2, bool allowSplit = false, int version = 1) => new()
    {
        ShiftTypeId = id,
        ShiftCode = code,
        ShiftName = name,
        StartTime = new TimeOnly(start, 0),
        EndTime = new TimeOnly(end, 0),
        IsActive = true,
        MaxShiftsPerEmployeePerDay = maxShifts,
        AllowSplitShift = allowSplit,
        ConfigurationVersion = version
    };

    private static List<ShiftType> CreateThreeFixedShifts() => new()
    {
        CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12),
        CreateShiftType(2, "AFTERNOON", "Ca Chiều", 12, 16),
        CreateShiftType(3, "EVENING", "Ca Tối", 16, 20)
    };
}
