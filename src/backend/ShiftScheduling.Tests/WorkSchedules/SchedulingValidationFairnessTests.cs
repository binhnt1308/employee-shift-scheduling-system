using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class SchedulingValidationFairnessTests
{
    private readonly SchedulingValidationService _service = new();

    [Fact(DisplayName = "TC-SCHEDULE-022 - Issue #12: Cảnh báo công bằng PT hiển thị giá trị số thực tế, không chứa literal {minPt}/{maxPt}")]
    public void ValidateSchedule_WhenPartTimeWorkloadUnbalanced_FairnessWarningContainsActualNumbersAndNoLiterals()
    {
        // Arrange
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();

        var emp1 = CreateEmployee(1, "PT01", "NV PT 1", EmploymentType.PartTime);
        var emp2 = CreateEmployee(2, "PT02", "NV PT 2", EmploymentType.PartTime);

        var dates = Enumerable.Range(0, 5).Select(i => period.WeekStartDate.AddDays(i)).ToList();
        var requirements = CreateFiveDayRequirements(dates, shiftTypes, day1Shift1RequiredSales: 1);

        // emp1 có 1 ca (dates[0]), emp2 có 4 ca (dates[1..4]) -> minPt = 1, maxPt = 4 (chênh lệch 3 > 2)
        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = dates[0], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[1], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[2], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[3], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[4], ShiftTypeId = 1 }
        };

        var avails = CreateAvailabilities(assignments);

        // Act
        var result = _service.ValidateSchedule(
            period, new[] { emp1, emp2 }, avails, new List<LeaveRequest>(), requirements, assignments, shiftTypes);

        // Assert
        Assert.Contains(result.Warnings, w => w.Contains("1 ca đến 4 ca"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("{minPt}") || w.Contains("{maxPt}"));
    }

    [Fact(DisplayName = "TC-SCHEDULE-023 - Issue #12: Cảnh báo công bằng FT hiển thị giá trị số thực tế, không chứa literal {minFt}/{maxFt}")]
    public void ValidateSchedule_WhenFullTimeWorkloadUnbalanced_FairnessWarningContainsActualNumbersAndNoLiterals()
    {
        // Arrange
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();

        var emp1 = CreateEmployee(1, "FT01", "NV FT 1", EmploymentType.FullTime);
        var emp2 = CreateEmployee(2, "FT02", "NV FT 2", EmploymentType.FullTime);

        var dates = Enumerable.Range(0, 5).Select(i => period.WeekStartDate.AddDays(i)).ToList();
        var requirements = CreateFiveDayRequirements(dates, shiftTypes, day1Shift1RequiredSales: 2);

        // emp1 làm 3 ngày (dates[0..2]), emp2 làm 5 ngày (dates[0..4]) -> minFt = 3, maxFt = 5 (chênh lệch 2 > 1)
        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = dates[0], ShiftTypeId = 1 },
            new() { EmployeeId = 1, WorkDate = dates[1], ShiftTypeId = 1 },
            new() { EmployeeId = 1, WorkDate = dates[2], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[0], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[1], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[2], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[3], ShiftTypeId = 1 },
            new() { EmployeeId = 2, WorkDate = dates[4], ShiftTypeId = 1 }
        };

        var avails = CreateAvailabilities(assignments);

        // Act
        var result = _service.ValidateSchedule(
            period, new[] { emp1, emp2 }, avails, new List<LeaveRequest>(), requirements, assignments, shiftTypes);

        // Assert
        Assert.Contains(result.Warnings, w => w.Contains("3 ngày đến 5 ngày"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("{minFt}") || w.Contains("{maxFt}"));
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

    private static SchedulePeriod CreateTestPeriod() => new()
    {
        SchedulePeriodId = 1,
        WeekStartDate = new DateOnly(2026, 8, 10),
        WeekEndDate = new DateOnly(2026, 8, 16)
    };

    private static List<ShiftType> CreateThreeFixedShifts() => new()
    {
        new()
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DurationHours = 4m,
            DisplayOrder = 1,
            IsActive = true,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false,
            ConfigurationVersion = 1
        },
        new()
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca chiều",
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(16, 0),
            DurationHours = 4m,
            DisplayOrder = 2,
            IsActive = true,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false,
            ConfigurationVersion = 1
        },
        new()
        {
            ShiftTypeId = 3,
            ShiftCode = "EVENING",
            ShiftName = "Ca tối",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(20, 0),
            DurationHours = 4m,
            DisplayOrder = 3,
            IsActive = true,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false,
            ConfigurationVersion = 1
        }
    };

    private static List<StaffingRequirement> CreateFiveDayRequirements(
        List<DateOnly> dates,
        List<ShiftType> shiftTypes,
        int day1Shift1RequiredSales)
    {
        var requirements = new List<StaffingRequirement>
        {
            new() { WorkDate = dates[0], ShiftTypeId = 1, ShiftType = shiftTypes[0], RequiredSalesStaff = day1Shift1RequiredSales, RequiredCashiers = 0 },
            new() { WorkDate = dates[0], ShiftTypeId = 2, ShiftType = shiftTypes[1], RequiredSalesStaff = 0, RequiredCashiers = 0 },
            new() { WorkDate = dates[0], ShiftTypeId = 3, ShiftType = shiftTypes[2], RequiredSalesStaff = 0, RequiredCashiers = 0 }
        };

        for (int i = 1; i < dates.Count; i++)
        {
            requirements.Add(new StaffingRequirement
            {
                WorkDate = dates[i],
                ShiftTypeId = 1,
                ShiftType = shiftTypes[0],
                RequiredSalesStaff = (day1Shift1RequiredSales == 2 && i < 3) ? 2 : 1,
                RequiredCashiers = 0
            });
        }

        return requirements;
    }

    private static List<Availability> CreateAvailabilities(IEnumerable<ShiftAssignment> assignments) =>
        assignments.Select(a => new Availability
        {
            EmployeeId = a.EmployeeId,
            WorkDate = a.WorkDate,
            ShiftTypeId = a.ShiftTypeId
        }).ToList();
}
