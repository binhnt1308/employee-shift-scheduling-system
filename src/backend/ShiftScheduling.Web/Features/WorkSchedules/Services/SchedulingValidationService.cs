using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Service kiểm tra toàn bộ các ràng buộc bắt buộc (BR02-BR13) và cảnh báo ưu tiên (BR18-BR19).
/// </summary>
public sealed class SchedulingValidationService : ISchedulingValidationService
{
    /// <inheritdoc />
    public ScheduleValidationResultViewModel ValidateSchedule(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyList<ShiftType> shiftTypes)
    {
        var hardErrors = new List<string>();
        var warnings = new List<string>();

        var availSet = availabilities
            .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId))
            .ToHashSet();
        var leaveLookup = new ApprovedLeaveLookup(approvedLeaves);
        var shiftTypeMap = shiftTypes.ToDictionary(s => s.ShiftTypeId);
        var empMap = activeEmployees.ToDictionary(e => e.EmployeeId);

        // BR03: Chỉ xử lý 3 ca cố định
        var fixedShiftIds = shiftTypes
            .Where(s => s.IsActive && IsFixedShiftCode(s.ShiftCode))
            .Select(s => s.ShiftTypeId)
            .ToHashSet();

        var validAssignments = assignments
            .Where(a => fixedShiftIds.Contains(a.ShiftTypeId))
            .ToList();

        // 1. Kiểm tra định mức nhân sự từng ca (BR12, BR13)
        ValidateStaffingRequirements(
            staffingRequirements, fixedShiftIds, validAssignments, empMap, hardErrors);

        // 2. Kiểm tra phân công, khối lượng công việc và nghỉ tối thiểu
        // (BR02, BR05, BR06, BR09-BR11, BR18, BR20, BR21)
        var summaries = ValidateEmployeesAndBuildSummaries(
            period, activeEmployees, validAssignments, availSet, leaveLookup, shiftTypeMap, hardErrors);

        // BR19: Cảnh báo công bằng khối lượng công việc giữa các nhân viên cùng hình thức
        AddFairnessWarnings(summaries, warnings);

        return new ScheduleValidationResultViewModel
        {
            IsValid = hardErrors.Count == 0,
            HardErrors = hardErrors.Distinct().ToList(),
            Warnings = warnings.Distinct().ToList(),
            EmployeeSummaries = summaries
        };
    }

    /// <summary>
    /// Kiểm tra định mức nhân sự theo vị trí (BR12) và yêu cầu nhân viên kinh nghiệm (BR13).
    /// </summary>
    private static void ValidateStaffingRequirements(
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        HashSet<int> fixedShiftIds,
        List<ShiftAssignment> validAssignments,
        Dictionary<int, Employee> empMap,
        List<string> hardErrors)
    {
        var reqs = staffingRequirements.Where(r => fixedShiftIds.Contains(r.ShiftTypeId));

        foreach (var req in reqs)
        {
            var shiftName = req.ShiftType?.ShiftName ?? $"Ca {req.ShiftTypeId}";
            var shiftAssigns = validAssignments
                .Where(a => a.WorkDate == req.WorkDate && a.ShiftTypeId == req.ShiftTypeId)
                .ToList();

            var salesCount = shiftAssigns.Count(
                a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Sales);
            var cashierCount = shiftAssigns.Count(
                a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Cashier);

            // BR12: Định mức nhân sự theo vị trí
            if (salesCount < req.RequiredSalesStaff)
            {
                hardErrors.Add($"Ngày {req.WorkDate:dd/MM} {shiftName}: " +
                    $"Thiếu {req.RequiredSalesStaff - salesCount} nhân viên Tư vấn.");
            }
            if (cashierCount < req.RequiredCashiers)
            {
                hardErrors.Add($"Ngày {req.WorkDate:dd/MM} {shiftName}: " +
                    $"Thiếu {req.RequiredCashiers - cashierCount} nhân viên Thu ngân.");
            }

            // BR13: Mỗi ca phải có ít nhất 1 nhân viên Level 2+ khi ca có yêu cầu nhân sự
            var hasLvl2Plus = shiftAssigns.Any(
                a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Level >= EmployeeLevel.Level2);
            if (!hasLvl2Plus && (req.RequiredSalesStaff + req.RequiredCashiers > 0))
            {
                hardErrors.Add($"Ngày {req.WorkDate:dd/MM} {shiftName}: " +
                    "Chưa có nhân viên kinh nghiệm (Level 2 trở lên).");
            }
        }
    }

    /// <summary>
    /// Kiểm tra quy tắc cá nhân (BR02, BR05, BR06, BR09-BR11, BR20, BR21)
    /// và xây dựng tóm tắt công việc (BR18).
    /// </summary>
    private static List<EmployeeSummaryItemViewModel> ValidateEmployeesAndBuildSummaries(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftAssignment> validAssignments,
        HashSet<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId)> availSet,
        ApprovedLeaveLookup leaveLookup,
        Dictionary<int, ShiftType> shiftTypeMap,
        List<string> hardErrors)
    {
        var empAssignments = validAssignments
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var summaries = new List<EmployeeSummaryItemViewModel>();

        foreach (var emp in activeEmployees)
        {
            var userAssigns = empAssignments.TryGetValue(emp.EmployeeId, out var list)
                ? list
                : new List<ShiftAssignment>();

            summaries.Add(BuildSingleEmployeeSummary(
                period, emp, userAssigns, availSet, leaveLookup, shiftTypeMap, hardErrors));
        }

        return summaries;
    }

    private static EmployeeSummaryItemViewModel BuildSingleEmployeeSummary(
        SchedulePeriod period,
        Employee emp,
        List<ShiftAssignment> userAssigns,
        HashSet<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId)> availSet,
        ApprovedLeaveLookup leaveLookup,
        Dictionary<int, ShiftType> shiftTypeMap,
        List<string> hardErrors)
    {
        var workDaysCount = userAssigns.Select(a => a.WorkDate).Distinct().Count();
        var totalShiftsCount = userAssigns.Count;

        var totalHours = userAssigns.Sum(
            assignment => shiftTypeMap.TryGetValue(assignment.ShiftTypeId, out var shiftType)
                ? shiftType.DurationHours
                : 0m);

        if (!emp.IsActive && userAssigns.Count > 0)
        {
            hardErrors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode})"
                + " đã ngưng hoạt động nhưng vẫn có ca.");
        }

        ValidateEmployeeAssignmentDetails(emp, userAssigns, availSet, leaveLookup, shiftTypeMap, hardErrors);
        ValidateEmployeeWorkloadRules(emp, userAssigns, shiftTypeMap, hardErrors);

        // BR-05: Tổng giờ làm trong tuần không được vượt trần theo loại hợp đồng.
        var hoursErrors = WeeklyWorkHoursRuleEvaluator.EvaluateWeeklyWorkHours(
            emp, userAssigns, shiftTypeMap, period.WeekStartDate, period.WeekEndDate);
        hardErrors.AddRange(hoursErrors);

        var restErrors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            emp, userAssigns, shiftTypeMap, period.WeekStartDate);
        hardErrors.AddRange(restErrors);

        var warningText = GetWorkloadWarning(emp, totalShiftsCount, workDaysCount);

        return new EmployeeSummaryItemViewModel
        {
            EmployeeId = emp.EmployeeId,
            EmployeeCode = emp.EmployeeCode,
            FullName = emp.FullName,
            Position = emp.Position,
            EmploymentType = emp.EmploymentType,
            Level = emp.Level,
            TotalShifts = totalShiftsCount,
            TotalWorkDays = workDaysCount,
            TotalHours = totalHours,
            Warning = warningText
        };
    }

    /// <summary>
    /// Kiểm tra đăng ký rảnh (BR05) và đơn xin nghỉ đã duyệt (BR06) từng ca của nhân viên.
    /// </summary>
    private static void ValidateEmployeeAssignmentDetails(
        Employee emp,
        List<ShiftAssignment> userAssigns,
        HashSet<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId)> availSet,
        ApprovedLeaveLookup leaveLookup,
        Dictionary<int, ShiftType> shiftTypeMap,
        List<string> hardErrors)
    {
        foreach (var assign in userAssigns)
        {
            var shiftName = shiftTypeMap.TryGetValue(assign.ShiftTypeId, out var st)
                ? st.ShiftName
                : $"Ca {assign.ShiftTypeId}";

            // BR05: Nhân viên chỉ được phân vào ca đã đăng ký rảnh
            if (!availSet.Contains((emp.EmployeeId, assign.WorkDate, assign.ShiftTypeId)))
            {
                hardErrors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode}) " +
                    $"chưa đăng ký rảnh cho ngày {assign.WorkDate:dd/MM} {shiftName}.");
            }

            // BR06: Đơn xin nghỉ đã duyệt (nghỉ cả ngày hoặc nghỉ theo ca)
            if (leaveLookup.IsOnApprovedLeave(emp.EmployeeId, assign.WorkDate, assign.ShiftTypeId))
            {
                hardErrors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode}) " +
                    $"có đơn nghỉ đã duyệt cho ngày {assign.WorkDate:dd/MM} {shiftName}.");
            }
        }
    }

    /// <summary>
    /// Kiểm tra quy tắc khối lượng công việc cho PT (BR09) và FT (BR10, BR11).
    /// </summary>
    private static void ValidateEmployeeWorkloadRules(
        Employee emp,
        List<ShiftAssignment> userAssigns,
        Dictionary<int, ShiftType> shiftTypeMap,
        List<string> hardErrors)
    {
        var dayGroups = userAssigns.GroupBy(a => a.WorkDate).ToList();

        if (emp.EmploymentType == EmploymentType.PartTime)
        {
            // BR09: Nhân viên bán thời gian chỉ được phân tối đa 1 ca/ngày
            foreach (var group in dayGroups)
            {
                if (group.Count() > 1)
                {
                    hardErrors.Add($"Nhân viên PT {emp.FullName} ({emp.EmployeeCode}) " +
                        $"bị phân {group.Count()} ca trong ngày {group.Key:dd/MM}.");
                }
            }
        }
        else if (emp.EmploymentType == EmploymentType.FullTime)
        {
            // BR11: Nhân viên toàn thời gian không được phân quá 6 ngày/tuần
            if (dayGroups.Count > SchedulingRuleConstants.FullTimeMaxWorkDaysPerWeek)
            {
                hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) làm " +
                    $"{dayGroups.Count} ngày trong tuần " +
                    $"(tối đa {SchedulingRuleConstants.FullTimeMaxWorkDaysPerWeek} ngày).");
            }

            // BR10: Nhân viên FT làm ngày nào phải đủ 2 ca liên tiếp (Sáng-Chiều hoặc Chiều-Tối)
            ValidateFullTimeShiftCombinations(emp, dayGroups, shiftTypeMap, hardErrors);
        }
    }

    /// <summary>
    /// Kiểm tra tổ hợp ca làm việc của nhân viên FT trong từng ngày làm việc (BR10).
    /// </summary>
    private static void ValidateFullTimeShiftCombinations(
        Employee emp,
        List<IGrouping<DateOnly, ShiftAssignment>> dayGroups,
        Dictionary<int, ShiftType> shiftTypeMap,
        List<string> hardErrors)
    {
        foreach (var group in dayGroups)
        {
            var codes = group
                .Select(a => shiftTypeMap.TryGetValue(a.ShiftTypeId, out var sc) ? sc.ShiftCode : string.Empty)
                .ToHashSet();

            var isMorningAfternoon = codes.Contains(ShiftTypeConstants.MorningCode)
                && codes.Contains(ShiftTypeConstants.AfternoonCode)
                && codes.Count == 2;

            var isAfternoonEvening = codes.Contains(ShiftTypeConstants.AfternoonCode)
                && codes.Contains(ShiftTypeConstants.EveningCode)
                && codes.Count == 2;

            if (!isMorningAfternoon && !isAfternoonEvening)
            {
                hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) " +
                    $"ngày {group.Key:dd/MM} không đạt tổ hợp 2 ca liên tiếp"
                        + " (Sáng-Chiều hoặc Chiều-Tối).");
            }
        }
    }

    /// <summary>
    /// Lấy chuỗi cảnh báo khối lượng công việc cá nhân (BR18).
    /// </summary>
    private static string GetWorkloadWarning(Employee emp, int totalShifts, int totalDays)
    {
        if (emp.EmploymentType == EmploymentType.PartTime)
        {
            var target = SchedulingRuleConstants.PartTimeTargetShiftsPerWeek;
            if (totalShifts == target) return $"Đạt mức dự kiến ({target} ca)";
            if (totalShifts < target) return $"Chưa đủ {target} ca (mới có {totalShifts} ca)";
            return $"Vượt mức {target} ca ({totalShifts} ca)";
        }
        else
        {
            var maxDays = SchedulingRuleConstants.FullTimeMaxWorkDaysPerWeek;
            if (totalDays == maxDays) return $"Đạt mức dự kiến ({maxDays} ngày / 48h)";
            if (totalDays < maxDays) return $"Chưa đủ {maxDays} ngày làm việc (mới có {totalDays} ngày)";
            return $"Vượt mức {maxDays} ngày ({totalDays} ngày)";
        }
    }

    /// <summary>
    /// Thêm cảnh báo về cán cân khối lượng công việc giữa các nhân viên cùng hình thức (BR19).
    /// </summary>
    private static void AddFairnessWarnings(
        List<EmployeeSummaryItemViewModel> summaries,
        List<string> warnings)
    {
        var ptSummaries = summaries.Where(s => s.EmploymentType == EmploymentType.PartTime).ToList();
        if (ptSummaries.Count > 1)
        {
            var maxPt = ptSummaries.Max(s => s.TotalShifts);
            var minPt = ptSummaries.Min(s => s.TotalShifts);
            if (maxPt - minPt > 2)
            {
                warnings.Add($"Chênh lệch số ca làm việc giữa các nhân viên PT lớn"
                    + " ({minPt} ca đến {maxPt} ca).");
            }
        }

        var ftSummaries = summaries.Where(s => s.EmploymentType == EmploymentType.FullTime).ToList();
        if (ftSummaries.Count > 1)
        {
            var maxFt = ftSummaries.Max(s => s.TotalWorkDays);
            var minFt = ftSummaries.Min(s => s.TotalWorkDays);
            if (maxFt - minFt > 1)
            {
                warnings.Add($"Chênh lệch số ngày làm việc giữa các nhân viên FT lớn"
                    + " ({minFt} ngày đến {maxFt} ngày).");
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem mã ca có thuộc 3 ca cố định của hệ thống theo BR03 hay không.
    /// </summary>
    private static bool IsFixedShiftCode(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode)) return false;
        return shiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cấu trúc tra cứu đơn xin nghỉ đã duyệt hiệu quả theo nhân viên, ngày và ca (BR06).
    /// </summary>
    private sealed class ApprovedLeaveLookup
    {
        private readonly HashSet<(int EmployeeId, DateOnly LeaveDate)> _fullDayLeaves = new();
        private readonly HashSet<(int EmployeeId, DateOnly LeaveDate, int ShiftTypeId)> _shiftLeaves = new();

        public ApprovedLeaveLookup(IEnumerable<LeaveRequest> approvedLeaves)
        {
            foreach (var leave in approvedLeaves)
            {
                if (leave.Status != LeaveRequestStatus.Approved)
                {
                    continue;
                }

                if (leave.ShiftTypeId.HasValue)
                {
                    _shiftLeaves.Add((leave.EmployeeId, leave.LeaveDate, leave.ShiftTypeId.Value));
                }
                else
                {
                    _fullDayLeaves.Add((leave.EmployeeId, leave.LeaveDate));
                }
            }
        }

        /// <summary>
        /// Kiểm tra nhân viên có đơn nghỉ đã duyệt trong ngày/ca chỉ định hay không (BR06).
        /// </summary>
        public bool IsOnApprovedLeave(int employeeId, DateOnly date, int shiftTypeId)
        {
            return _fullDayLeaves.Contains((employeeId, date))
                || _shiftLeaves.Contains((employeeId, date, shiftTypeId));
        }
    }
}
