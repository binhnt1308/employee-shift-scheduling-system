using Google.OrTools.Sat;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public sealed partial class OrToolsSchedulingService
{
    private static void AddAvailabilityAndLeaveConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        HashSet<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId)> availSet,
        List<LeaveRequest> leaveSet)
    {
        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            for (int d = 0; d < 7; d++)
            {
                var workDate = period.WeekStartDate.AddDays(d);
                for (int s = 0; s < 3; s++)
                {
                    var shift = fixedShifts[s];
                    x[e, d, s] = model.NewBoolVar($"x_{emp.EmployeeId}_{d}_{shift.ShiftTypeId}");

                    var isAvail = availSet.Contains((emp.EmployeeId, workDate, shift.ShiftTypeId));
                    var isOnLeave = leaveSet.Any(l => l.EmployeeId == emp.EmployeeId
                        && l.LeaveDate == workDate
                        && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == shift.ShiftTypeId));

                    if (!isAvail || isOnLeave || !emp.IsActive)
                    {
                        model.Add(x[e, d, s] == 0);
                    }
                }
            }
        }
    }

    private static void AddStaffingRequirementsConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        Dictionary<(DateOnly WorkDate, int ShiftTypeId), StaffingRequirement> reqMap)
    {
        int empCount = activeEmployees.Count;
        for (int d = 0; d < 7; d++)
        {
            var workDate = period.WeekStartDate.AddDays(d);
            for (int s = 0; s < 3; s++)
            {
                var shift = fixedShifts[s];
                reqMap.TryGetValue((workDate, shift.ShiftTypeId), out var req);
                int salesReq = req?.RequiredSalesStaff ?? 0;
                int cashierReq = req?.RequiredCashiers ?? 0;

                var salesVars = new List<BoolVar>();
                var cashierVars = new List<BoolVar>();
                var lvl2PlusVars = new List<BoolVar>();

                for (int e = 0; e < empCount; e++)
                {
                    var emp = activeEmployees[e];
                    if (emp.Position == EmployeePosition.Sales) salesVars.Add(x[e, d, s]);
                    else if (emp.Position == EmployeePosition.Cashier) cashierVars.Add(x[e, d, s]);

                    if (emp.Level >= EmployeeLevel.Level2) lvl2PlusVars.Add(x[e, d, s]);
                }

                model.Add(LinearExpr.Sum(salesVars) == salesReq);
                model.Add(LinearExpr.Sum(cashierVars) == cashierReq);

                if (salesReq + cashierReq > 0)
                {
                    model.Add(LinearExpr.Sum(lvl2PlusVars) >= 1);
                }
            }
        }
    }

    private static void AddEmploymentWorkloadConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        int morningIdx,
        int afternoonIdx,
        int eveningIdx)
    {
        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            if (emp.EmploymentType == EmploymentType.PartTime)
            {
                for (int d = 0; d < 7; d++)
                {
                    model.Add(x[e, d, 0] + x[e, d, 1] + x[e, d, 2] <= 1);
                }
            }
            else if (emp.EmploymentType == EmploymentType.FullTime)
            {
                for (int d = 0; d < 7; d++)
                {
                    model.Add(x[e, d, morningIdx] + x[e, d, eveningIdx] <= 1);
                    model.Add(x[e, d, morningIdx] <= x[e, d, afternoonIdx]);
                    model.Add(x[e, d, eveningIdx] <= x[e, d, afternoonIdx]);
                    model.Add(x[e, d, morningIdx] + x[e, d, eveningIdx] == x[e, d, afternoonIdx]);
                }

                var ftDayVars = new List<BoolVar>();
                for (int d = 0; d < 7; d++)
                {
                    ftDayVars.Add(x[e, d, afternoonIdx]);
                }
                model.Add(LinearExpr.Sum(ftDayVars) <= 6);
            }
        }
    }

    /// <summary>
    /// Thêm ràng buộc trần tổng giờ làm việc trong tuần theo loại hợp đồng (BR-05 / NC-01).
    /// Sử dụng đơn vị phút nguyên chính xác từ DurationHours để tương thích với CP-SAT solver.
    /// </summary>
    private static void AddWeeklyMaxHoursConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftType> fixedShifts)
    {
        var shiftDurationMinutes = new long[fixedShifts.Count];
        for (int s = 0; s < fixedShifts.Count; s++)
        {
            var totalMinutes = fixedShifts[s].DurationHours * 60m;
            shiftDurationMinutes[s] = decimal.ToInt64(totalMinutes);
        }

        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            var maxMinutes = emp.EmploymentType == EmploymentType.FullTime
                ? decimal.ToInt64(SchedulingRuleConstants.FullTimeMaxWeeklyWorkHours * 60m)
                : decimal.ToInt64(SchedulingRuleConstants.PartTimeMaxWeeklyWorkHours * 60m);

            var vars = new List<BoolVar>();
            var coeffs = new List<long>();

            for (int d = 0; d < 7; d++)
            {
                for (int s = 0; s < fixedShifts.Count; s++)
                {
                    vars.Add(x[e, d, s]);
                    coeffs.Add(shiftDurationMinutes[s]);
                }
            }

            // BR-05: Tổng giờ làm trong tuần không được vượt trần theo loại hợp đồng.
            model.Add(LinearExpr.WeightedSum(vars, coeffs) <= maxMinutes);
        }
    }

    private static void AddWeeklyContinuousRestConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        SchedulePeriod period,
        List<ShiftType> fixedShifts)
    {
        var minRestHours = SchedulingRuleConstants.MinimumWeeklyContinuousRestHours;
        var periodStart = period.WeekStartDate.ToDateTime(TimeOnly.MinValue);
        var periodEnd = periodStart.AddDays(7);

        var candidateStarts = new HashSet<DateTime> { periodStart };
        for (int d = 0; d < 7; d++)
        {
            var workDate = period.WeekStartDate.AddDays(d);
            foreach (var shift in fixedShifts)
            {
                var shiftEnd = workDate.ToDateTime(shift.EndTime);
                if (shift.EndTime <= shift.StartTime)
                {
                    shiftEnd = shiftEnd.AddDays(1);
                }
                candidateStarts.Add(shiftEnd);
            }
        }

        var candidateWindows = new List<(DateTime Start, DateTime End)>();
        foreach (var start in candidateStarts.OrderBy(t => t))
        {
            var end = start.AddHours(minRestHours);
            if (end <= periodEnd)
            {
                candidateWindows.Add((start, end));
            }
        }

        if (candidateWindows.Count == 0)
        {
            return;
        }

        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var windowVars = new List<BoolVar>();
            for (int k = 0; k < candidateWindows.Count; k++)
            {
                var (winStart, winEnd) = candidateWindows[k];
                var winVar = model.NewBoolVar($"rest_win_{e}_{k}");
                windowVars.Add(winVar);

                for (int d = 0; d < 7; d++)
                {
                    var workDate = period.WeekStartDate.AddDays(d);
                    for (int s = 0; s < 3; s++)
                    {
                        var shift = fixedShifts[s];
                        var shiftStart = workDate.ToDateTime(shift.StartTime);
                        var shiftEnd = workDate.ToDateTime(shift.EndTime);
                        if (shiftEnd <= shiftStart)
                        {
                            shiftEnd = shiftEnd.AddDays(1);
                        }

                        if (shiftStart < winEnd && shiftEnd > winStart)
                        {
                            model.Add(x[e, d, s] == 0).OnlyEnforceIf(winVar);
                        }
                    }
                }
            }

            model.AddBoolOr(windowVars);
        }
    }

    private static void AddWorkloadObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees)
    {
        int empCount = activeEmployees.Count;
        var objTerms = new List<LinearExpr>();

        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            var allEmpShifts = new List<BoolVar>();
            for (int d = 0; d < 7; d++)
            {
                for (int s = 0; s < 3; s++)
                {
                    allEmpShifts.Add(x[e, d, s]);
                }
            }

            var totalEmpShifts = LinearExpr.Sum(allEmpShifts);
            int targetShifts = emp.EmploymentType == EmploymentType.PartTime ? 5 : 12;

            var diff = model.NewIntVar(-20, 20, $"diff_{e}");
            model.Add(diff == totalEmpShifts - targetShifts);

            var absDiff = model.NewIntVar(0, 20, $"abs_diff_{e}");
            model.AddAbsEquality(absDiff, diff);
            objTerms.Add(absDiff * 10);
        }

        model.Minimize(LinearExpr.Sum(objTerms));
    }
}
