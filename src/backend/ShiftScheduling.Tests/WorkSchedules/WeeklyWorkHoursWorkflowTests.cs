using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class WeeklyWorkHoursWorkflowTests
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

    public WeeklyWorkHoursWorkflowTests()
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

    [Fact(DisplayName = "TC-NC01-BR05-006 - Manual assign exceeding PT 28 hours (25h+5h=30h) returns failure with BR-05")]
    public async Task AssignEmployeeManualAsync_WhenPartTimeExceeds28Hours_FailsOnlyBR05()
    {
        var period = CreateWeekPeriod();
        _periodRepo.Add(period);
        var pt = CreateEmployee(2, "PT01", "PartTime Emp", EmploymentType.PartTime);
        _employeeRepo.Employees.Add(pt);

        var morning5h = new ShiftType { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng 5h", DurationHours = 5m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(13, 0) };
        _shiftTypeRepo.ShiftTypes.Add(morning5h);

        var schedule = new WorkSchedule { WorkScheduleId = 1, SchedulePeriodId = period.SchedulePeriodId, Status = ScheduleStatus.Draft };
        for (int d = 0; d < 5; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            _availRepo.Availabilities.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 1 });
            schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
        }
        _workScheduleRepo.Schedules.Add(schedule);

        var day5 = period.WeekStartDate.AddDays(5);
        _availRepo.Availabilities.Add(new Availability { AvailabilityId = 99, EmployeeId = pt.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = day5, ShiftTypeId = 1 });

        var model = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = pt.EmployeeId,
            WorkDate = day5,
            ShiftTypeId = 1
        };

        var result = await _scheduleService.AssignEmployeeManualAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
        Assert.Contains("BR-05", result.ErrorMessage ?? string.Empty);
        Assert.Contains("30", result.ErrorMessage ?? string.Empty);
        Assert.Contains("28", result.ErrorMessage ?? string.Empty);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Equal(5, schedule.ShiftAssignments.Count);
    }

    [Fact(DisplayName = "TC-NC01-BR05-007 - Manual replace does not double count hours")]
    public async Task AssignEmployeeManualAsync_WhenReplacing_DoesNotDoubleCount()
    {
        var period = CreateWeekPeriod();
        _periodRepo.Add(period);
        var pt = CreateEmployee(2, "PT01", "PartTime Emp", EmploymentType.PartTime);
        _employeeRepo.Employees.Add(pt);
        _shiftTypeRepo.ShiftTypes.AddRange(CreateFixedShifts());

        var schedule = new WorkSchedule { WorkScheduleId = 1, SchedulePeriodId = period.SchedulePeriodId, Status = ScheduleStatus.Draft };
        for (int d = 0; d < 3; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            _availRepo.Availabilities.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 1 });
            schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
        }
        for (int d = 4; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            _availRepo.Availabilities.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 3 });
            schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = date, ShiftTypeId = 3 });
        }
        _workScheduleRepo.Schedules.Add(schedule);

        var model = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = pt.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 1,
            ReplaceShiftAssignmentId = 1
        };

        var result = await _scheduleService.AssignEmployeeManualAsync(model, 10);
        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "TC-NC01-BR05-008 - Manual replace exceeding FT 48 hours (48h-4h+5h=49h) returns failure with BR-05")]
    public async Task AssignEmployeeManualAsync_WhenFullTimeReplaceExceeds48Hours_FailsOnlyBR05()
    {
        var period = CreateWeekPeriod();
        _periodRepo.Add(period);
        var ft = CreateEmployee(1, "FT01", "FullTime Emp", EmploymentType.FullTime);
        _employeeRepo.Employees.Add(ft);

        var shifts = CreateFixedShifts();
        shifts.Add(new ShiftType { ShiftTypeId = 4, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng 5h", DurationHours = 5m, DisplayOrder = 4, IsActive = true, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(12, 0) });
        _shiftTypeRepo.ShiftTypes.AddRange(shifts);

        var schedule = new WorkSchedule { WorkScheduleId = 1, SchedulePeriodId = period.SchedulePeriodId, Status = ScheduleStatus.Draft };
        for (int d = 0; d < 6; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            _availRepo.Availabilities.Add(new Availability { AvailabilityId = d * 2 + 1, EmployeeId = ft.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 1 });
            _availRepo.Availabilities.Add(new Availability { AvailabilityId = d * 2 + 2, EmployeeId = ft.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 2 });
            schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = d * 2 + 1, EmployeeId = ft.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
            schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = d * 2 + 2, EmployeeId = ft.EmployeeId, WorkDate = date, ShiftTypeId = 2 });
        }
        _workScheduleRepo.Schedules.Add(schedule);
        _availRepo.Availabilities.Add(new Availability { AvailabilityId = 99, EmployeeId = ft.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = period.WeekStartDate, ShiftTypeId = 4 });

        var model = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = ft.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 4,
            ReplaceShiftAssignmentId = 1
        };

        var result = await _scheduleService.AssignEmployeeManualAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
        Assert.Contains("BR-05", result.ErrorMessage ?? string.Empty);
        Assert.Contains("49", result.ErrorMessage ?? string.Empty);
        Assert.Contains("48", result.ErrorMessage ?? string.Empty);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Contains(schedule.ShiftAssignments, a => a.ShiftAssignmentId == 1 && a.ShiftTypeId == 1);
    }

    [Fact(DisplayName = "TC-NC01-BR05-009 - Publish validation rejects schedule exceeding FT 48 hours (5 days x 10h = 50h)")]
    public async Task PublishScheduleAsync_WhenFullTimeExceedsHoursLimit_RejectsPublishOnlyBR05()
    {
        var period = CreateWeekPeriod();
        _periodRepo.Add(period);
        var ft = CreateEmployee(1, "FT01", "FullTime Emp", EmploymentType.FullTime);
        _employeeRepo.Employees.Add(ft);

        var morning5h = new ShiftType { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng 5h", DurationHours = 5m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(12, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 };
        var afternoon5h = new ShiftType { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều 5h", DurationHours = 5m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(17, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 };
        var evening5h = new ShiftType { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối 4h", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 };
        _shiftTypeRepo.ShiftTypes.AddRange(new[] { morning5h, afternoon5h, evening5h });

        period.StaffingRequirements.Clear();
        for (int d = 0; d < 5; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            period.StaffingRequirements.Add(new StaffingRequirement { SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 1, ShiftType = morning5h, RequiredSalesStaff = 1, RequiredCashiers = 0, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
            period.StaffingRequirements.Add(new StaffingRequirement { SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 2, ShiftType = afternoon5h, RequiredSalesStaff = 1, RequiredCashiers = 0, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
            period.StaffingRequirements.Add(new StaffingRequirement { SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 3, ShiftType = evening5h, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
        }

        var schedule = new WorkSchedule { WorkScheduleId = 1, SchedulePeriodId = period.SchedulePeriodId, Status = ScheduleStatus.Draft };
        for (int d = 0; d < 5; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            _availRepo.Availabilities.Add(new Availability { AvailabilityId = d * 2 + 1, EmployeeId = ft.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 1 });
            _availRepo.Availabilities.Add(new Availability { AvailabilityId = d * 2 + 2, EmployeeId = ft.EmployeeId, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 2 });
            schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = d * 2 + 1, EmployeeId = ft.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
            schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = d * 2 + 2, EmployeeId = ft.EmployeeId, WorkDate = date, ShiftTypeId = 2 });
        }
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _scheduleService.PublishScheduleAsync(period.SchedulePeriodId, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePublish, result.ErrorCode);
        Assert.Contains("BR-05", result.ErrorMessage ?? string.Empty);
        Assert.Equal(ScheduleStatus.Draft, schedule.Status);
        Assert.Null(schedule.PublishedAt);
    }

    [Fact(DisplayName = "TC-NC01-BR05-010 - Shift transfer submit rejects receiver exceeding max hours")]
    public async Task ShiftTransfer_SubmitAsync_WhenReceiverExceedsMaxHours_RejectsAndLeavesEmployeeUnchanged()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.EmploymentType = EmploymentType.PartTime;
        context.Morning.DurationHours = 5m;
        context.TargetAssignment.ShiftType.DurationHours = 5m;

        for (int d = 1; d <= 5; d++)
        {
            context.AddReceiverAssignment(context.Period.WeekStartDate.AddDays(d), context.Morning);
        }

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("BR-05", result.ErrorMessage ?? string.Empty);
        Assert.Equal(context.Sender.EmployeeId, context.TargetAssignment.EmployeeId);
    }

    [Fact(DisplayName = "TC-NC01-BR05-011 - Shift transfer manager review rejects receiver exceeding max hours and preserves pending state")]
    public async Task ShiftTransfer_ReviewAsync_WhenReceiverExceedsMaxHours_RejectsAndPreservesState()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.EmploymentType = EmploymentType.PartTime;
        context.Morning.DurationHours = 5m;
        context.TargetAssignment.ShiftType.DurationHours = 5m;

        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);
        for (int d = 1; d <= 5; d++)
        {
            context.AddReceiverAssignment(context.Period.WeekStartDate.AddDays(d), context.Morning);
        }

        var result = await context.Service.ReviewAsync(request.ShiftTransferRequestId, true, null, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("BR-05", result.ErrorMessage ?? string.Empty);
        Assert.Contains("30", result.ErrorMessage ?? string.Empty);
        Assert.Contains("28", result.ErrorMessage ?? string.Empty);
        Assert.Equal(context.Sender.EmployeeId, context.TargetAssignment.EmployeeId);
        Assert.Equal(ApprovalStatus.Pending, request.ApprovalStatus);
        Assert.Null(request.ReviewedAt);
        Assert.Equal(0, context.UnitOfWork.SaveCount);
        Assert.Empty(context.AuditLogs.Items);
        Assert.Empty(context.Notifications.Items);
    }

    [Fact(DisplayName = "TC-NC01-BR05-012 - Solver returns INFEASIBLE when PartTime forced 30h > 28h")]
    public void Solver_ReturnsInfeasible_WhenPartTimeRequires30Hours()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(1, "PT01", "PartTime 1", EmploymentType.PartTime);
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng 5h", DurationHours = 5m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(13, 0) },
            new() { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều 4h", DurationHours = 4m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0) },
            new() { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối 4h", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0) }
        };

        var avails = new List<Availability>();
        var reqs = new List<StaffingRequirement>();
        for (int d = 0; d < 6; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            avails.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1 });
            reqs.Add(new StaffingRequirement { SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1, RequiredSalesStaff = 1, RequiredCashiers = 0 });
        }

        var result = _orToolsService.GenerateProposal(period, new[] { pt }, avails, new List<LeaveRequest>(), reqs, shifts, 10);

        Assert.False(result.Succeeded);
        Assert.Equal("INFEASIBLE", result.Status);
    }

    [Fact(DisplayName = "TC-NC01-BR05-013 - Solver returns INFEASIBLE when FullTime forced 50h > 48h")]
    public void Solver_ReturnsInfeasible_WhenFullTimeRequires50Hours()
    {
        var period = CreateWeekPeriod();
        var ft = CreateEmployee(1, "FT01", "FullTime 1", EmploymentType.FullTime);
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng 5h", DurationHours = 5m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(12, 0) },
            new() { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều 5h", DurationHours = 5m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(17, 0) },
            new() { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối 4h", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0) }
        };

        var avails = new List<Availability>();
        var reqs = new List<StaffingRequirement>();
        int id = 1;
        for (int d = 0; d < 5; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            avails.Add(new Availability { AvailabilityId = id++, EmployeeId = ft.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1 });
            avails.Add(new Availability { AvailabilityId = id++, EmployeeId = ft.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 2 });
            reqs.Add(new StaffingRequirement { SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1, RequiredSalesStaff = 1, RequiredCashiers = 0 });
            reqs.Add(new StaffingRequirement { SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 2, RequiredSalesStaff = 1, RequiredCashiers = 0 });
        }

        var result = _orToolsService.GenerateProposal(period, new[] { ft }, avails, new List<LeaveRequest>(), reqs, shifts, 10);

        Assert.False(result.Succeeded);
        Assert.Equal("INFEASIBLE", result.Status);
    }

    [Fact(DisplayName = "TC-NC01-BR05-014 - Solver succeeds and respects max weekly hours for feasible staffing")]
    public void Solver_Succeeds_WhenStaffingIsFeasibleUnderHoursLimits()
    {
        var period = CreateWeekPeriod();
        var ptEmployees = new List<Employee>
        {
            CreateEmployee(1, "PT01", "PartTime 1", EmploymentType.PartTime),
            CreateEmployee(2, "PT02", "PartTime 2", EmploymentType.PartTime),
            CreateEmployee(3, "PT03", "PartTime 3", EmploymentType.PartTime),
            CreateEmployee(4, "PT04", "PartTime 4", EmploymentType.PartTime)
        };
        var shifts = CreateFixedShifts();
        var avails = new List<Availability>();
        int availId = 1;
        foreach (var emp in ptEmployees)
        {
            for (int d = 0; d < 7; d++)
            {
                for (int s = 1; s <= 3; s++)
                {
                    avails.Add(new Availability { AvailabilityId = availId++, EmployeeId = emp.EmployeeId, SchedulePeriodId = 1, WorkDate = period.WeekStartDate.AddDays(d), ShiftTypeId = s });
                }
            }
        }
        var reqs = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            for (int s = 1; s <= 3; s++)
            {
                reqs.Add(new StaffingRequirement { SchedulePeriodId = 1, WorkDate = period.WeekStartDate.AddDays(d), ShiftTypeId = s, RequiredSalesStaff = 1, RequiredCashiers = 0 });
            }
        }

        var result = _orToolsService.GenerateProposal(period, ptEmployees, avails, new List<LeaveRequest>(), reqs, shifts, 10);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Status.Equals("OPTIMAL", StringComparison.OrdinalIgnoreCase) || result.Status.Equals("FEASIBLE", StringComparison.OrdinalIgnoreCase));

        foreach (var emp in ptEmployees)
        {
            var empAssigns = result.ProposedAssignments.Where(a => a.EmployeeId == emp.EmployeeId).ToList();
            var hours = empAssigns.Sum(a => shifts.First(s => s.ShiftTypeId == a.ShiftTypeId).DurationHours);
            Assert.True(hours <= 28m);
        }
    }

    private static SchedulePeriod CreateWeekPeriod()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Tuần 1",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };
        var shifts = CreateFixedShifts();
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

    private static Employee CreateEmployee(int id, string code, string name, EmploymentType type) => new()
    {
        EmployeeId = id,
        EmployeeCode = code,
        FullName = name,
        EmploymentType = type,
        Position = EmployeePosition.Sales,
        Level = EmployeeLevel.Level2,
        IsActive = true
    };

    private static List<ShiftType> CreateFixedShifts() => new()
    {
        new() { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng", DurationHours = 4m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
        new() { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều", DurationHours = 4m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
        new() { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 }
    };
}
