using Google.OrTools.Sat;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public sealed partial class OrToolsSchedulingService
{
    /// <summary>
    /// Trọng số cơ sở cho mỗi lần nhân viên phải làm ca tối (EVENING) liên tiếp giữa 2 ngày kế cận (ngày D và D+1).
    /// Đây là tầng ưu tiên thứ 3 trong hàm mục tiêu BR-10.
    /// </summary>
    private const long ConsecutiveEveningShiftBaseWeight = 1L;

    /// <summary>
    /// Thêm hàm mục tiêu tối ưu hóa mềm (BR-10):
    /// 1. Cân đối tổng số phút làm việc thực tế giữa các nhân viên cùng loại hợp đồng (FT vs FT, PT vs PT).
    /// 2. Tôn trọng mức nguyện vọng đăng ký rảnh (Preferred có chi phí tốt hơn Normal).
    /// 3. Phạt ca tối liên tiếp (EVENING ngày D + EVENING ngày D+1) mà không làm phát sinh vô nghiệm.
    /// Các trọng số được tính toán phân cấp (hierarchical) theo kích thước bài toán để đảm bảo thứ tự ưu tiên tuyệt đối.
    /// </summary>
    private static void AddWorkloadObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftType> fixedShifts,
        SchedulePeriod period,
        IReadOnlyList<Availability> availabilities,
        int eveningIdx)
    {
        int empCount = activeEmployees.Count;
        int shiftCount = fixedShifts.Count;
        var objTerms = new List<LinearExpr>();

        // Tính toán các trọng số phân cấp (Lexicographic / Hierarchical weights) dựa trên giới hạn trên đóng góp tối đa:
        // Tầng 3: Phạt ca tối liên tiếp
        long consecutiveEveningWeight = ConsecutiveEveningShiftBaseWeight;
        long maxConsecutiveEveningContribution = (long)empCount * 6L * consecutiveEveningWeight;

        // Tầng 2: Ưu tiên nguyện vọng Preferred so với Normal (hoặc không xác định).
        // Phải lớn hơn toàn bộ đóng góp tối đa của ca tối liên tiếp cộng lại:
        long normalPreferenceWeight = maxConsecutiveEveningContribution + 1L;
        long maxPreferenceContribution = (long)empCount * 7L * (long)shiftCount * normalPreferenceWeight;

        // Tầng 1: Cân bằng tổng phút làm việc giữa các nhân viên cùng loại hợp đồng (FT-FT, PT-PT).
        // Một đơn vị cải thiện (1 phút) phải lớn hơn toàn bộ đóng góp tối đa của cả preference và ca tối cộng lại:
        long workloadMinuteDiscrepancyWeight = maxPreferenceContribution + maxConsecutiveEveningContribution + 1L;

        // 1. Tính tổng số phút làm việc thực tế của từng nhân viên dựa trên DurationHours của ShiftType
        var shiftDurationMinutes = new long[fixedShifts.Count];
        for (int s = 0; s < fixedShifts.Count; s++)
        {
            var totalMinutes = fixedShifts[s].DurationHours * 60m;
            shiftDurationMinutes[s] = decimal.ToInt64(totalMinutes);
        }

        const long maxWeeklyMinutes = 7L * 24L * 60L; // 10,080 phút
        var empTotalMinutesVars = new IntVar[empCount];

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
        }

        // Cân đối giờ công theo cặp nhân viên cùng loại hợp đồng: FT so với FT, PT so với PT
        var ftIndices = new List<int>();
        var ptIndices = new List<int>();
        for (int e = 0; e < empCount; e++)
        {
            if (activeEmployees[e].EmploymentType == EmploymentType.FullTime)
            {
                ftIndices.Add(e);
            }
            else if (activeEmployees[e].EmploymentType == EmploymentType.PartTime)
            {
                ptIndices.Add(e);
            }
        }

        AddPairwiseWorkloadBalanceTerms(model, ftIndices, empTotalMinutesVars, activeEmployees, "ft", workloadMinuteDiscrepancyWeight, objTerms);
        AddPairwiseWorkloadBalanceTerms(model, ptIndices, empTotalMinutesVars, activeEmployees, "pt", workloadMinuteDiscrepancyWeight, objTerms);

        // 2. Tôn trọng mức nguyện vọng đăng ký rảnh (PreferenceLevel)
        // Ca Preferred có chi phí 0; Ca Normal (hoặc giá trị không xác định) chịu chi phí normalPreferenceWeight
        var prefMap = new Dictionary<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel>();
        foreach (var a in availabilities)
        {
            prefMap[(a.EmployeeId, a.WorkDate, a.ShiftTypeId)] = a.PreferenceLevel;
        }

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

                    // Nếu là Preferred -> chi phí tốt hơn (0). Nếu là Normal hoặc giá trị không hợp lệ -> phạt chi phí normalPreferenceWeight
                    bool isPreferred = prefLevel == AvailabilityPreferenceLevel.Preferred;
                    if (!isPreferred)
                    {
                        objTerms.Add(x[e, d, s] * normalPreferenceWeight);
                    }
                }
            }
        }

        // 3. Hạn chế xếp ca tối liên tiếp (EVENING ngày D và EVENING ngày D+1)
        if (eveningIdx >= 0 && eveningIdx < fixedShifts.Count)
        {
            for (int e = 0; e < empCount; e++)
            {
                var emp = activeEmployees[e];
                for (int d = 0; d < 6; d++)
                {
                    var consecEvening = model.NewBoolVar($"consec_eve_{emp.EmployeeId}_{d}");
                    model.Add(consecEvening >= x[e, d, eveningIdx] + x[e, d + 1, eveningIdx] - 1);
                    objTerms.Add(consecEvening * consecutiveEveningWeight);
                }
            }
        }

        if (objTerms.Count > 0)
        {
            model.Minimize(LinearExpr.Sum(objTerms));
        }
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
                    $"abs_workload_diff_{groupPrefix}_{activeEmployees[e1].EmployeeId}_{activeEmployees[e2].EmployeeId}");
                model.AddAbsEquality(absDiff, diff);

                objTerms.Add(absDiff * workloadWeight);
            }
        }
    }
}
