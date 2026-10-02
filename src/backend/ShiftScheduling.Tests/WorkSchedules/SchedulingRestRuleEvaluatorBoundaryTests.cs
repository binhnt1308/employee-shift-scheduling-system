using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.WorkSchedules;

/// <summary>
/// Kiểm tra các mốc ranh giới thời gian (LC-04) của quy tắc nghỉ giữa hai ca (BR20).
/// </summary>
public sealed class SchedulingRestRuleEvaluatorBoundaryTests
{
    [Fact(DisplayName = "TC-LC04-01 - BR20 - Bien cuoi thang (31/01 -> 01/02) nghi dung 12 gio -> Pass")]
    public void Evaluate_MonthBoundaryRest_ReturnsNoErrors()
    {
        var weekStart = new DateOnly(2026, 1, 26);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var evening = CreateShiftType(3, "EVENING", "Ca Tối", new TimeOnly(16, 0), new TimeOnly(20, 0));
        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 3, evening } };

        var emp = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV001",
            FullName = "Nguyen Van A",
            EmploymentType = EmploymentType.PartTime,
            IsActive = true
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment
            {
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 1, 31),
                ShiftTypeId = 3
            },
            new ShiftAssignment
            {
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 2, 1),
                ShiftTypeId = 1
            }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            emp, assignments, shiftMap, weekStart);

        Assert.DoesNotContain(errors, e => e.Contains("BR20"));
    }

    [Fact(DisplayName = "TC-LC04-02 - BR20 - Bien cuoi nam (31/12 -> 01/01) nghi dung 12 gio -> Pass")]
    public void Evaluate_YearBoundaryRest_ReturnsNoErrors()
    {
        var weekStart = new DateOnly(2026, 12, 28);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var evening = CreateShiftType(3, "EVENING", "Ca Tối", new TimeOnly(16, 0), new TimeOnly(20, 0));
        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 3, evening } };

        var emp = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV001",
            FullName = "Nguyen Van A",
            EmploymentType = EmploymentType.PartTime,
            IsActive = true
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment
            {
                EmployeeId = 1,
                WorkDate = new DateOnly(2026, 12, 31),
                ShiftTypeId = 3
            },
            new ShiftAssignment
            {
                EmployeeId = 1,
                WorkDate = new DateOnly(2027, 1, 1),
                ShiftTypeId = 1
            }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            emp, assignments, shiftMap, weekStart);

        Assert.DoesNotContain(errors, e => e.Contains("BR20"));
    }

    [Fact(DisplayName = "TC-LC04-03 - BR20 - Bien 29/02 nam nhuan 2028 (28/02 -> 29/02) nghi dung 12 gio -> Pass")]
    public void Evaluate_LeapDayBoundaryRest_ReturnsNoErrors()
    {
        var weekStart = new DateOnly(2028, 2, 28);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var evening = CreateShiftType(3, "EVENING", "Ca Tối", new TimeOnly(16, 0), new TimeOnly(20, 0));
        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 3, evening } };

        var emp = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV001",
            FullName = "Nguyen Van A",
            EmploymentType = EmploymentType.PartTime,
            IsActive = true
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment
            {
                EmployeeId = 1,
                WorkDate = new DateOnly(2028, 2, 28),
                ShiftTypeId = 3
            },
            new ShiftAssignment
            {
                EmployeeId = 1,
                WorkDate = new DateOnly(2028, 2, 29),
                ShiftTypeId = 1
            }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            emp, assignments, shiftMap, weekStart);

        Assert.DoesNotContain(errors, e => e.Contains("BR20"));
    }

    private static ShiftType CreateShiftType(
        int id, string code, string name, TimeOnly start, TimeOnly end)
    {
        return new ShiftType
        {
            ShiftTypeId = id,
            ShiftCode = code,
            ShiftName = name,
            StartTime = start,
            EndTime = end,
            DurationHours = 4m,
            IsActive = true
        };
    }
}
