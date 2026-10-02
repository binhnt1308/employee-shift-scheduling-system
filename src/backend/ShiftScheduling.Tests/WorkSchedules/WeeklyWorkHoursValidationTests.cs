using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class WeeklyWorkHoursValidationTests
{
    private readonly SchedulingValidationService _validationService = new();

    [Fact(DisplayName = "TC-NC01-BR05-001 - FT exactly 48 hours is valid")]
    public void ValidateSchedule_WhenFullTimeExactly48Hours_IsValid()
    {
        var period = CreateWeekPeriod();
        var ft = CreateEmployee(1, "FT01", "FullTime Emp", EmploymentType.FullTime);
        var shifts = CreateFixedShifts();
        var (assignments, availabilities) = CreateFtAssignments(ft, period.WeekStartDate, 6);

        var result = _validationService.ValidateSchedule(
            period, new[] { ft }, availabilities, new List<LeaveRequest>(),
            CreateRequirementsFromAssignments(assignments, shifts), assignments, shifts);

        Assert.True(result.IsValid);
        Assert.Empty(result.HardErrors);
        var summary = Assert.Single(result.EmployeeSummaries);
        Assert.Equal(48m, summary.TotalHours);
    }

    [Fact(DisplayName = "TC-NC01-BR05-002 - FT exceeds 48 hours produces hard error")]
    public void ValidateSchedule_WhenFullTimeExceeds48Hours_ReturnsHardError()
    {
        var period = CreateWeekPeriod();
        var ft = CreateEmployee(1, "FT01", "FullTime Emp", EmploymentType.FullTime);
        var shifts = CreateFixedShifts();
        var (assignments, availabilities) = CreateFtAssignments(ft, period.WeekStartDate, 7);

        var result = _validationService.ValidateSchedule(
            period, new[] { ft }, availabilities, new List<LeaveRequest>(),
            CreateRequirementsFromAssignments(assignments, shifts), assignments, shifts);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("BR-05") && e.Contains("48"));
    }

    [Fact(DisplayName = "TC-NC01-BR05-003 - PT exactly 28 hours is valid")]
    public void ValidateSchedule_WhenPartTimeExactly28Hours_IsValid()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(2, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var shifts = CreateFixedShifts();

        var assignments = new List<ShiftAssignment>();
        var avails = new List<Availability>();

        // Thứ Hai đến Thứ Tư: làm ca Sáng 08:00-12:00.
        for (int d = 0; d < 3; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            avails.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1 });
            assignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
        }
        // Thứ Năm đến Chủ Nhật: làm ca Tối 16:00-20:00; từ 12:00 Thứ Tư đến 16:00 Thứ Năm có 28 giờ nghỉ.
        for (int d = 3; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            avails.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 3 });
            assignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = date, ShiftTypeId = 3 });
        }

        var result = _validationService.ValidateSchedule(
            period, new[] { pt }, avails, new List<LeaveRequest>(),
            CreateRequirementsFromAssignments(assignments, shifts), assignments, shifts);

        Assert.True(result.IsValid);
        Assert.Empty(result.HardErrors);
        var summary = Assert.Single(result.EmployeeSummaries);
        Assert.Equal(28m, summary.TotalHours);
    }

    [Fact(DisplayName = "TC-NC01-BR05-004 - PT exceeds 28 hours produces hard error")]
    public void ValidateSchedule_WhenPartTimeExceeds28Hours_ReturnsHardError()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(2, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var customShift = new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca Sáng", DurationHours = 5m, IsActive = true };
        var shifts = new List<ShiftType> { customShift };
        var assignments = new List<ShiftAssignment>();
        for (int d = 0; d < 6; d++)
        {
            assignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = period.WeekStartDate.AddDays(d), ShiftTypeId = 1 });
        }

        var errors = WeeklyWorkHoursRuleEvaluator.EvaluateWeeklyWorkHours(
            pt, assignments, shifts.ToDictionary(s => s.ShiftTypeId), period.WeekStartDate, period.WeekEndDate);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("BR-05") && e.Contains("28"));
    }

    [Fact(DisplayName = "TC-NC01-BR05-005 - PT under target 20 hours is not blocked")]
    public void ValidateSchedule_WhenPartTimeUnder20Hours_NoHardError()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(2, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var shifts = CreateFixedShifts();
        var (assignments, availabilities) = CreatePtAssignments(pt, period.WeekStartDate, 3);

        var result = _validationService.ValidateSchedule(
            period, new[] { pt }, availabilities, new List<LeaveRequest>(),
            CreateRequirementsFromAssignments(assignments, shifts), assignments, shifts);

        Assert.True(result.IsValid);
        Assert.Empty(result.HardErrors);
        var summary = Assert.Single(result.EmployeeSummaries);
        Assert.Equal(12m, summary.TotalHours);
    }

    [Fact(DisplayName = "TC-NC01-BR05-015 - Assignments out of period are not counted into weekly hours")]
    public void EvaluateWeeklyWorkHours_WhenAssignmentsOutOfPeriod_IgnoresOutOfPeriodAssignments()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(1, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var shifts = CreateFixedShifts();
        var assignments = new List<ShiftAssignment>
        {
            new() { ShiftAssignmentId = 1, EmployeeId = 1, WorkDate = period.WeekStartDate.AddDays(-1), ShiftTypeId = 1 },
            new() { ShiftAssignmentId = 2, EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new() { ShiftAssignmentId = 3, EmployeeId = 1, WorkDate = period.WeekEndDate.AddDays(1), ShiftTypeId = 1 }
        };

        var totalHours = WeeklyWorkHoursRuleEvaluator.CalculateTotalHours(
            assignments, shifts.ToDictionary(s => s.ShiftTypeId), period.WeekStartDate, period.WeekEndDate);

        Assert.Equal(4m, totalHours);
    }

    [Fact(DisplayName = "TC-NC01-BR05-016 - Unknown shift type handled safely without exception")]
    public void EvaluateWeeklyWorkHours_WhenShiftTypeUnknown_DoesNotThrow()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(1, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var assignments = new List<ShiftAssignment>
        {
            new() { ShiftAssignmentId = 1, EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 999 }
        };

        var errors = WeeklyWorkHoursRuleEvaluator.EvaluateWeeklyWorkHours(
            pt, assignments, new Dictionary<int, ShiftType>(), period.WeekStartDate, period.WeekEndDate);

        Assert.Empty(errors);
    }

    [Fact(DisplayName = "TC-NC01-BR05-017 - ValidateSchedule uses real DurationHours and fails BR-05 with 5h shifts")]
    public void ValidateSchedule_WhenPartTime6Days5Hours_FailsBR05InValidationService()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(1, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng 5h", DurationHours = 5m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(13, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều", DurationHours = 4m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 }
        };

        var assignments = new List<ShiftAssignment>();
        var avails = new List<Availability>();
        for (int d = 0; d < 6; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            avails.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1 });
            assignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
        }

        var result = _validationService.ValidateSchedule(
            period, new[] { pt }, avails, new List<LeaveRequest>(),
            CreateRequirementsFromAssignments(assignments, shifts), assignments, shifts);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("BR-05") && e.Contains("30") && e.Contains("28"));
    }

    [Fact(DisplayName = "TC-NC01-BR05-018 - Solver rejects duration configuration that cannot be integer minutes")]
    public void GenerateProposal_WhenShiftDurationCannotBeIntegerMinutes_ReturnsInvalidConfiguration()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(1, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var invalidShifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng", DurationHours = 4.333m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 20) },
            new() { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều", DurationHours = 4m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(12, 20), EndTime = new TimeOnly(16, 20) },
            new() { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(16, 20), EndTime = new TimeOnly(20, 20) }
        };

        var solver = new OrToolsSchedulingService(_validationService, NullLogger<OrToolsSchedulingService>.Instance);
        var result = solver.GenerateProposal(period, new[] { pt }, new List<Availability>(), new List<LeaveRequest>(), new List<StaffingRequirement>(), invalidShifts, 5);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_CONFIGURATION", result.Status);
        Assert.Contains("không tương ứng với số phút nguyên", result.Message);
    }

    [Fact(DisplayName = "TC-NC01-BR05-019 - Solver accurately converts 4.5h duration to 270 minutes and solves")]
    public void GenerateProposal_WhenShiftDurationIs4Point5Hours_ConvertsExactlyTo270Minutes()
    {
        var period = CreateWeekPeriod();
        var ft = CreateEmployee(1, "FT01", "FullTime Emp", EmploymentType.FullTime);
        var ft2 = CreateEmployee(2, "FT02", "FullTime Emp 2", EmploymentType.FullTime);
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng 4.5h", DurationHours = 4.5m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(7, 30), EndTime = new TimeOnly(12, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều 4.5h", DurationHours = 4.5m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 30), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối 4h", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(16, 30), EndTime = new TimeOnly(20, 30), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 }
        };

        var avails = new List<Availability>();
        int id = 1;
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                avails.Add(new Availability { AvailabilityId = id++, EmployeeId = 1, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = s });
                avails.Add(new Availability { AvailabilityId = id++, EmployeeId = 2, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = s });
            }
        }

        var reqs = new List<StaffingRequirement>
        {
            new() { SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1, RequiredSalesStaff = 1, RequiredCashiers = 0 },
            new() { SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 2, RequiredSalesStaff = 1, RequiredCashiers = 0 },
            new() { SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 3, RequiredSalesStaff = 0, RequiredCashiers = 0 }
        };

        var solver = new OrToolsSchedulingService(_validationService, NullLogger<OrToolsSchedulingService>.Instance);
        var result = solver.GenerateProposal(period, new[] { ft, ft2 }, avails, new List<LeaveRequest>(), reqs, shifts, 10);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.Status.Equals("OPTIMAL", StringComparison.OrdinalIgnoreCase) || result.Status.Equals("FEASIBLE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "TC-NC01-BR05-020 - Solver rejects DurationHours of 0 despite valid StartTime and EndTime without fallback")]
    public void GenerateProposal_WhenShiftDurationIsZero_ReturnsInvalidConfigurationWithoutFallback()
    {
        var period = CreateWeekPeriod();
        var pt = CreateEmployee(1, "PT01", "PartTime Emp", EmploymentType.PartTime);
        var shiftsWithZeroDuration = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca Sáng", DurationHours = 0m, DisplayOrder = 1, IsActive = true, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
            new() { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca Chiều", DurationHours = 4m, DisplayOrder = 2, IsActive = true, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) },
            new() { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca Tối", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0) }
        };

        var solver = new OrToolsSchedulingService(_validationService, NullLogger<OrToolsSchedulingService>.Instance);
        var result = solver.GenerateProposal(period, new[] { pt }, new List<Availability>(), new List<LeaveRequest>(), new List<StaffingRequirement>(), shiftsWithZeroDuration, 5);

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_CONFIGURATION", result.Status);
        Assert.Contains("Ca Sáng", result.Message);
        Assert.Contains("không hợp lệ", result.Message);
    }

    private static SchedulePeriod CreateWeekPeriod() => new()
    {
        SchedulePeriodId = 1,
        PeriodName = "Tuần 1",
        WeekStartDate = new DateOnly(2026, 8, 10),
        WeekEndDate = new DateOnly(2026, 8, 16)
    };

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

    private static List<StaffingRequirement> CreateRequirementsFromAssignments(
        IEnumerable<ShiftAssignment> assignments,
        IEnumerable<ShiftType> shifts)
    {
        var shiftList = shifts.ToList();
        var reqs = new List<StaffingRequirement>();
        var assignGroup = assignments
            .GroupBy(a => (a.WorkDate, a.ShiftTypeId))
            .ToDictionary(g => g.Key, g => g.Count());

        var dates = assignments.Select(a => a.WorkDate).Distinct().ToList();
        if (dates.Count == 0) dates.Add(new DateOnly(2026, 8, 10));

        foreach (var date in dates)
        {
            foreach (var s in shiftList)
            {
                int count = assignGroup.TryGetValue((date, s.ShiftTypeId), out var c) ? c : 0;
                reqs.Add(new StaffingRequirement
                {
                    SchedulePeriodId = 1,
                    WorkDate = date,
                    ShiftTypeId = s.ShiftTypeId,
                    ShiftType = s,
                    RequiredSalesStaff = count,
                    RequiredCashiers = 0,
                    RequiredExperiencedStaff = count > 0 ? 1 : 0,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }
        return reqs;
    }

    private static (List<ShiftAssignment>, List<Availability>) CreatePtAssignments(Employee pt, DateOnly start, int dayCount)
    {
        var assignments = new List<ShiftAssignment>();
        var avails = new List<Availability>();
        for (int d = 0; d < dayCount; d++)
        {
            var date = start.AddDays(d);
            avails.Add(new Availability { AvailabilityId = d + 1, EmployeeId = pt.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1 });
            assignments.Add(new ShiftAssignment { ShiftAssignmentId = d + 1, EmployeeId = pt.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
        }
        return (assignments, avails);
    }

    private static (List<ShiftAssignment>, List<Availability>) CreateFtAssignments(Employee ft, DateOnly start, int dayCount)
    {
        var assignments = new List<ShiftAssignment>();
        var avails = new List<Availability>();
        int assignId = 1;
        for (int d = 0; d < dayCount; d++)
        {
            var date = start.AddDays(d);
            avails.Add(new Availability { AvailabilityId = assignId, EmployeeId = ft.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1 });
            assignments.Add(new ShiftAssignment { ShiftAssignmentId = assignId++, EmployeeId = ft.EmployeeId, WorkDate = date, ShiftTypeId = 1 });
            avails.Add(new Availability { AvailabilityId = assignId, EmployeeId = ft.EmployeeId, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 2 });
            assignments.Add(new ShiftAssignment { ShiftAssignmentId = assignId++, EmployeeId = ft.EmployeeId, WorkDate = date, ShiftTypeId = 2 });
        }
        return (assignments, avails);
    }
}
