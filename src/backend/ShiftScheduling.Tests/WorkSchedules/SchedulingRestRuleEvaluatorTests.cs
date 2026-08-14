using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class SchedulingRestRuleEvaluatorTests
{
    private readonly DateOnly _weekStart = new(2026, 8, 10); // Thứ 2

    private static Employee CreateEmployee(int id, EmploymentType type) => new()
    {
        EmployeeId = id,
        EmployeeCode = $"NV{id:D2}",
        FullName = $"Nhân viên {id}",
        EmploymentType = type,
        IsActive = true
    };

    private static ShiftType CreateShiftType(int id, string code, string name, TimeOnly start, TimeOnly end) => new()
    {
        ShiftTypeId = id,
        ShiftCode = code,
        ShiftName = name,
        StartTime = start,
        EndTime = end,
        DurationHours = (decimal)(end - start).TotalHours,
        IsActive = true
    };

    [Fact(DisplayName = "TC-BR20-01 - BR20 - Nghỉ đúng 12 giờ giữa ca cuối ngày D" +
        " và ca đầu ngày D+1 -> Pass")]
    public void Evaluate_BR20_Exact12HoursRest_ReturnsNoErrors()
    {
        var emp = CreateEmployee(1, EmploymentType.PartTime);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var evening = CreateShiftType(3, "EVENING", "Ca Tối", new TimeOnly(16, 0), new TimeOnly(20, 0));

        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 3, evening } };

        // Ngày 1 ca Tối (kết thúc 20:00), Ngày 2 ca Sáng (bắt đầu 08:00) -> Khoảng nghỉ = 12h
        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = _weekStart, ShiftTypeId = 3 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(1), ShiftTypeId = 1 }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(emp, assignments, shiftMap, _weekStart);

        Assert.DoesNotContain(errors, e => e.Contains("BR20"));
    }

    [Fact(DisplayName = "TC-BR20-02 - BR20 - Nghỉ lớn hơn 12 giờ (16 giờ)" +
        " giữa hai ngày liên tiếp -> Pass")]
    public void Evaluate_BR20_MoreThan12HoursRest_ReturnsNoErrors()
    {
        var emp = CreateEmployee(1, EmploymentType.PartTime);
        var afternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều", new TimeOnly(12, 0), new TimeOnly(16, 0));
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));

        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 2, afternoon } };

        // Ngày 1 ca Chiều (kết thúc 16:00), Ngày 2 ca Sáng (bắt đầu 08:00) -> Khoảng nghỉ = 16h
        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = _weekStart, ShiftTypeId = 2 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(1), ShiftTypeId = 1 }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(emp, assignments, shiftMap, _weekStart);

        Assert.DoesNotContain(errors, e => e.Contains("BR20"));
    }

    [Fact(DisplayName = "TC-BR20-03 - BR20 - Nghỉ nhỏ hơn 12 giờ (11 giờ)" +
        " giữa hai ngày (Synthetic Shift) -> Fail")]
    public void Evaluate_BR20_LessThan12HoursRest_ReturnsHardError()
    {
        var emp = CreateEmployee(1, EmploymentType.PartTime);
        var lateShift = CreateShiftType(10, "LATE", "Ca Muộn", new TimeOnly(13, 0), new TimeOnly(21, 0));
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));

        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 10, lateShift } };

        // Ngày 1 ca Muộn (kết thúc 21:00), Ngày 2 ca Sáng (bắt đầu 08:00) -> Khoảng nghỉ = 11h
        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = _weekStart, ShiftTypeId = 10 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(1), ShiftTypeId = 1 }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(emp, assignments, shiftMap, _weekStart);

        Assert.Contains(errors, e => e.Contains("BR20") && e.Contains("không đủ 12 giờ nghỉ"));
    }

    [Fact(DisplayName = "TC-BR21-01 - BR21 - Có đúng một khoảng nghỉ 24 giờ liên tục trong kỳ -> Pass")]
    public void Evaluate_BR21_Exact24HoursContinuousRest_ReturnsNoErrors()
    {
        var emp = CreateEmployee(1, EmploymentType.PartTime);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var afternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều", new TimeOnly(12, 0), new TimeOnly(16, 0));
        var evening = CreateShiftType(3, "EVENING", "Ca Tối", new TimeOnly(16, 0), new TimeOnly(20, 0));
        var shiftMap = new Dictionary<int, ShiftType>
        {
            { 1, morning }, { 2, afternoon }, { 3, evening }
        };

        // Gap0 (T2 00:00 -> T2 16:00) = 16h < 24h
        // Gap1..4 (T2-T6 ca Tối 16:00-20:00) = 20h < 24h
        // Gap5 (T6 Tối -> T7 Sáng 08:00-12:00) = 12h < 24h
        // Gap6 (T7 Sáng 12:00 -> CN Chiều 12:00-16:00) = 24.0h (duy nhất)
        // GapLast (CN Chiều 16:00 -> T2 tuần sau 00:00) = 8h < 24h
        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = _weekStart, ShiftTypeId = 3 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(1), ShiftTypeId = 3 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(2), ShiftTypeId = 3 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(3), ShiftTypeId = 3 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(4), ShiftTypeId = 3 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(5), ShiftTypeId = 1 },
            new() { EmployeeId = 1, WorkDate = _weekStart.AddDays(6), ShiftTypeId = 2 }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(emp, assignments, shiftMap, _weekStart);

        Assert.DoesNotContain(errors, e => e.Contains("BR21"));
    }

    [Fact(DisplayName = "TC-BR21-02 - BR21 - PT làm ca Sáng cả 7 ngày trong kỳ (Max gap 20h < 24h) -> Fail")]
    public void Evaluate_BR21_PartTime7DaysNo24hGap_ReturnsHardError()
    {
        var emp = CreateEmployee(1, EmploymentType.PartTime);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning } };

        // PT làm ca Sáng 08:00-12:00 cả 7 ngày trong tuần
        var assignments = Enumerable.Range(0, 7)
            .Select(d => new ShiftAssignment { EmployeeId = 1, WorkDate = _weekStart.AddDays(d), ShiftTypeId = 1 })
            .ToList();

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(emp, assignments, shiftMap, _weekStart);

        Assert.Contains(errors, e => e.Contains("BR21") && e.Contains("không có ít nhất 24 giờ nghỉ"));
    }

    [Fact(DisplayName = "TC-BR21-03 - BR21 - FT làm ca 7 ngày không có khoảng nghỉ 24 giờ -> Fail")]
    public void Evaluate_BR21_FullTimeNo24hGap_ReturnsHardError()
    {
        var emp = CreateEmployee(2, EmploymentType.FullTime);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var afternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều", new TimeOnly(12, 0), new TimeOnly(16, 0));
        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 2, afternoon } };

        // FT làm Sáng + Chiều cả 7 ngày (08:00 - 16:00 mỗi ngày) -> gap giữa các ngày = 16h < 24h
        var assignments = new List<ShiftAssignment>();
        for (int d = 0; d < 7; d++)
        {
            var date = _weekStart.AddDays(d);
            assignments.Add(new ShiftAssignment { EmployeeId = 2, WorkDate = date, ShiftTypeId = 1 });
            assignments.Add(new ShiftAssignment { EmployeeId = 2, WorkDate = date, ShiftTypeId = 2 });
        }

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(emp, assignments, shiftMap, _weekStart);

        Assert.Contains(errors, e => e.Contains("BR21"));
    }

    [Fact(DisplayName = "TC-BR21-04 - BR21 - FT làm 6 ngày, nghỉ 1 ngày Chủ Nhật (Gap = 28h > 24h) -> Pass")]
    public void Evaluate_BR21_FullTime6DaysWithSundayOff_ReturnsNoErrors()
    {
        var emp = CreateEmployee(2, EmploymentType.FullTime);
        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var afternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều", new TimeOnly(12, 0), new TimeOnly(16, 0));
        var shiftMap = new Dictionary<int, ShiftType> { { 1, morning }, { 2, afternoon } };

        // FT làm Thứ 2 -> Thứ 7 ca Sáng+Chiều (kết thúc Thứ 7 lúc 16:00), Chủ Nhật nghỉ
        var assignments = new List<ShiftAssignment>();
        for (int d = 0; d < 6; d++)
        {
            var date = _weekStart.AddDays(d);
            assignments.Add(new ShiftAssignment { EmployeeId = 2, WorkDate = date, ShiftTypeId = 1 });
            assignments.Add(new ShiftAssignment { EmployeeId = 2, WorkDate = date, ShiftTypeId = 2 });
        }

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(emp, assignments, shiftMap, _weekStart);

        Assert.DoesNotContain(errors, e => e.Contains("BR21"));
    }
}
