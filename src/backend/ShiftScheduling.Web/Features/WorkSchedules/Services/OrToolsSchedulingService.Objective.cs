using Google.OrTools.Sat;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public sealed partial class OrToolsSchedulingService
{
    private const long ConsecutiveEveningShiftBaseWeight = 1L;

    /// <summary>
    /// Thêm hàm mục tiêu tối ưu hóa mềm (BR-10) phân cấp theo thứ tự ưu tiên tuyệt đối:
    /// 1. Cân đối tổng thời lượng làm việc giữa các nhân viên cùng loại hợp đồng.
    /// 2. Tôn trọng mức nguyện vọng đăng ký rảnh (Preferred có chi phí tốt hơn Normal).
    /// 3. Ưu tiên ghép ca liên tiếp cùng ngày cho nhân viên toàn thời gian (FT).
    /// 4. Hạn chế xếp ca tối liên tiếp (EVENING ngày D + EVENING ngày D+1) giữa hai ngày kế cận.
    /// Ràng buộc bắt buộc (hard constraints) luôn có ưu tiên tối thượng và không thể vi phạm.
    /// </summary>
    private static void AddWorkloadObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftType> fixedShifts,
        SchedulePeriod period,
        IReadOnlyList<Availability> availabilities,
        int morningIdx,
        int afternoonIdx,
        int eveningIdx)
    {
        var objTerms = new List<LinearExpr>();
        var weights = ComputeObjectiveWeights(activeEmployees.Count, fixedShifts.Count);

        AddWorkloadFairnessObjective(
            model, x, activeEmployees, fixedShifts, weights.WorkloadWeight, objTerms);
        AddAvailabilityPreferenceObjective(
            model, x, activeEmployees, fixedShifts, period, availabilities, weights.PreferenceWeight, objTerms);
        AddFullTimeConsecutivePairObjective(
            model, x, activeEmployees, morningIdx, afternoonIdx, eveningIdx, weights.FtUnpairedWeight, objTerms);
        AddConsecutiveEveningObjective(
            model, x, activeEmployees, fixedShifts.Count, eveningIdx, weights.EveningWeight, objTerms);

        if (objTerms.Count > 0)
        {
            model.Minimize(LinearExpr.Sum(objTerms));
        }
    }

    /// <summary>
    /// Tính trọng số theo phân cấp để tổng ảnh hưởng của mọi mục tiêu ưu tiên thấp
    /// không thể lấn át một đơn vị của mục tiêu ưu tiên cao hơn.
    /// </summary>
    private static (long WorkloadWeight, long PreferenceWeight, long FtUnpairedWeight, long EveningWeight)
        ComputeObjectiveWeights(int empCount, int shiftCount)
    {
        long eveningWeight = ConsecutiveEveningShiftBaseWeight;
        long maxEveningContribution = (long)empCount * 6L * eveningWeight;

        long ftUnpairedWeight = maxEveningContribution + 1L;
        long maxFtUnpairedContribution = (long)empCount * 7L * 2L * ftUnpairedWeight;

        long preferenceWeight = maxFtUnpairedContribution + maxEveningContribution + 1L;
        long maxPreferenceContribution = (long)empCount * 7L * (long)shiftCount * preferenceWeight;

        long workloadWeight = maxPreferenceContribution + maxFtUnpairedContribution + maxEveningContribution + 1L;

        return (workloadWeight, preferenceWeight, ftUnpairedWeight, eveningWeight);
    }

    /// <summary>
    /// Cân bằng tổng thời lượng làm việc giữa các nhân viên cùng nhóm FT hoặc PT.
    /// Tiêu chí này đo fairness theo thời lượng, không phải theo số ngày làm việc.
    /// </summary>
    private static void AddWorkloadFairnessObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftType> fixedShifts,
        long workloadWeight,
        List<LinearExpr> objTerms)
    {
        var shiftDurationMinutes = new long[fixedShifts.Count];
        for (int s = 0; s < fixedShifts.Count; s++)
        {
            var totalMinutes = fixedShifts[s].DurationHours * 60m;
            shiftDurationMinutes[s] = decimal.ToInt64(totalMinutes);
        }

        const long maxWeeklyMinutes = 7L * 24L * 60L;
        int empCount = activeEmployees.Count;
        var empTotalMinutesVars = new IntVar[empCount];
        var ftIndices = new List<int>();
        var ptIndices = new List<int>();

        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            var minutesTerms = new List<LinearExpr>();
            for (int d = 0; d < 7; d++)
            {
                for (int s = 0; s < fixedShifts.Count; s++)
                {
                    minutesTerms.Add(x[e, d, s] * shiftDurationMinutes[s]);
                }
            }

            var totalMinVar = model.NewIntVar(0, maxWeeklyMinutes, $"total_mins_{emp.EmployeeId}");
            model.Add(totalMinVar == LinearExpr.Sum(minutesTerms));
            empTotalMinutesVars[e] = totalMinVar;

            if (emp.EmploymentType == EmploymentType.FullTime) ftIndices.Add(e);
            else if (emp.EmploymentType == EmploymentType.PartTime) ptIndices.Add(e);
        }

        AddPairwiseWorkloadBalanceTerms(
            model, ftIndices, empTotalMinutesVars, activeEmployees, "ft", workloadWeight, objTerms);
        AddPairwiseWorkloadBalanceTerms(
            model, ptIndices, empTotalMinutesVars, activeEmployees, "pt", workloadWeight, objTerms);
    }

    private static void AddPairwiseWorkloadBalanceTerms(
        CpModel model,
        List<int> groupIndices,
        IntVar[] empTotalMinutesVars,
        IReadOnlyList<Employee> activeEmployees,
        string groupPrefix,
        long workloadWeight,
        List<LinearExpr> objTerms)
    {
        const long maxWeeklyMinutes = 7L * 24L * 60L;

        for (int i = 0; i < groupIndices.Count; i++)
        {
            int e1 = groupIndices[i];
            for (int j = i + 1; j < groupIndices.Count; j++)
            {
                int e2 = groupIndices[j];

                var diff = model.NewIntVar(
                    -maxWeeklyMinutes,
                    maxWeeklyMinutes,
                    $"workload_diff_{groupPrefix}_{activeEmployees[e1].EmployeeId}_{activeEmployees[e2].EmployeeId}");
                model.Add(diff == empTotalMinutesVars[e1] - empTotalMinutesVars[e2]);

                var absDiff = model.NewIntVar(
                    0,
                    maxWeeklyMinutes,
                    $"abs_diff_{groupPrefix}_{activeEmployees[e1].EmployeeId}_{activeEmployees[e2].EmployeeId}");
                model.AddAbsEquality(absDiff, diff);

                objTerms.Add(absDiff * workloadWeight);
            }
        }
    }

    private static void AddAvailabilityPreferenceObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftType> fixedShifts,
        SchedulePeriod period,
        IReadOnlyList<Availability> availabilities,
        long preferenceWeight,
        List<LinearExpr> objTerms)
    {
        var prefMap = new Dictionary<(int, DateOnly, int), AvailabilityPreferenceLevel>();
        foreach (var a in availabilities)
        {
            prefMap[(a.EmployeeId, a.WorkDate, a.ShiftTypeId)] = a.PreferenceLevel;
        }

        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            for (int d = 0; d < 7; d++)
            {
                var workDate = period.WeekStartDate.AddDays(d);
                for (int s = 0; s < fixedShifts.Count; s++)
                {
                    var shift = fixedShifts[s];
                    prefMap.TryGetValue((emp.EmployeeId, workDate, shift.ShiftTypeId), out var prefLevel);

                    if (prefLevel != AvailabilityPreferenceLevel.Preferred)
                    {
                        objTerms.Add(x[e, d, s] * preferenceWeight);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Ưu tiên mềm việc ghép hai ca liền nhau trong cùng ngày cho nhân viên FT
    /// khi phương án vẫn thỏa các ràng buộc cứng và mục tiêu ưu tiên cao hơn.
    /// Việc ghép ca không phải là điều kiện bắt buộc để lịch được xem là hợp lệ.
    /// </summary>
    private static void AddFullTimeConsecutivePairObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        int morningIdx,
        int afternoonIdx,
        int eveningIdx,
        long ftUnpairedWeight,
        List<LinearExpr> objTerms)
    {
        if (morningIdx < 0 || afternoonIdx < 0 || eveningIdx < 0)
        {
            return;
        }

        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            if (emp.EmploymentType != EmploymentType.FullTime)
            {
                continue;
            }

            for (int d = 0; d < 7; d++)
            {
                var morningAfternoonPair = model.NewBoolVar($"ft_pair_ma_{emp.EmployeeId}_{d}");
                model.Add(morningAfternoonPair <= x[e, d, morningIdx]);
                model.Add(morningAfternoonPair <= x[e, d, afternoonIdx]);
                model.Add(morningAfternoonPair >= x[e, d, morningIdx] + x[e, d, afternoonIdx] - 1);

                var afternoonEveningPair = model.NewBoolVar($"ft_pair_ae_{emp.EmployeeId}_{d}");
                model.Add(afternoonEveningPair <= x[e, d, afternoonIdx]);
                model.Add(afternoonEveningPair <= x[e, d, eveningIdx]);
                model.Add(afternoonEveningPair >= x[e, d, afternoonIdx] + x[e, d, eveningIdx] - 1);

                // Chỉ phạt số ca FT còn lẻ để phương án ghép hai ca liền nhau có chi phí 0,
                // solver ưu tiên gom ca mà không biến việc ghép ca thành ràng buộc bắt buộc.
                var dailyAssigned =
                    x[e, d, morningIdx]
                    + x[e, d, afternoonIdx]
                    + x[e, d, eveningIdx];
                var unpairedShifts = model.NewIntVar(0, 3, $"ft_unpaired_{emp.EmployeeId}_{d}");
                model.Add(unpairedShifts == dailyAssigned - 2 * (morningAfternoonPair + afternoonEveningPair));

                objTerms.Add(unpairedShifts * ftUnpairedWeight);
            }
        }
    }

    /// <summary>
    /// Giảm việc xếp cùng một nhân viên vào ca tối ở hai ngày liên tiếp khi các
    /// phương án ưu tiên cao hơn có chất lượng tương đương.
    /// </summary>
    private static void AddConsecutiveEveningObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        int shiftCount,
        int eveningIdx,
        long eveningWeight,
        List<LinearExpr> objTerms)
    {
        if (eveningIdx < 0 || eveningIdx >= shiftCount)
        {
            return;
        }

        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            for (int d = 0; d < 6; d++)
            {
                var consecEvening = model.NewBoolVar($"consec_eve_{emp.EmployeeId}_{d}");
                model.Add(consecEvening >= x[e, d, eveningIdx] + x[e, d + 1, eveningIdx] - 1);
                objTerms.Add(consecEvening * eveningWeight);
            }
        }
    }
}
