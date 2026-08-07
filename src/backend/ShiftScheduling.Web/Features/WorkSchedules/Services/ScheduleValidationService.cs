using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Service kiem tra toan bo cac rang buoc bat buoc (BR02-BR13) va canh bao uuu tien (BR18-BR19).
/// </summary>
public sealed class ScheduleValidationService : IScheduleValidationService
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

        var availMap = availabilities.ToHashSet(new AvailKeyComparer());
        var leaveSet = approvedLeaves.Where(l => l.Status == LeaveRequestStatus.Approved).ToList();
        var shiftTypeMap = shiftTypes.ToDictionary(s => s.ShiftTypeId);
        var empMap = activeEmployees.ToDictionary(e => e.EmployeeId);

        // BR03: Chi xu ly 3 ca co dinh
        var fixedShiftIds = shiftTypes
            .Where(s => s.IsActive && IsFixedShiftCode(s.ShiftCode))
            .Select(s => s.ShiftTypeId)
            .ToHashSet();

        var validAssignments = assignments
            .Where(a => fixedShiftIds.Contains(a.ShiftTypeId))
            .ToList();

        // 1. Kiem tra dinh muc nhan su tung ca (BR12, BR13)
        foreach (var req in staffingRequirements.Where(r => fixedShiftIds.Contains(r.ShiftTypeId)))
        {
            var shiftName = req.ShiftType?.ShiftName ?? $"Ca {req.ShiftTypeId}";
            var shiftAssigns = validAssignments
                .Where(a => a.WorkDate == req.WorkDate && a.ShiftTypeId == req.ShiftTypeId)
                .ToList();

            var salesCount = shiftAssigns.Count(a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Sales);
            var cashierCount = shiftAssigns.Count(a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Cashier);

            // BR12: Định mức nhân sự theo vị trí
            if (salesCount < req.RequiredSalesStaff)
            {
                hardErrors.Add($"Ngày {req.WorkDate:dd/MM} {shiftName}: Thiếu {req.RequiredSalesStaff - salesCount} nhân viên Tư vấn.");
            }
            if (cashierCount < req.RequiredCashiers)
            {
                hardErrors.Add($"Ngày {req.WorkDate:dd/MM} {shiftName}: Thiếu {req.RequiredCashiers - cashierCount} nhân viên Thu ngân.");
            }

            // BR13: Mỗi ca phải có ít nhất 1 nhân viên Level 2+
            var hasLvl2Plus = shiftAssigns.Any(a => empMap.TryGetValue(a.EmployeeId, out var e) && e.Level >= EmployeeLevel.Level2);
            if (!hasLvl2Plus && (req.RequiredSalesStaff + req.RequiredCashiers > 0))
            {
                hardErrors.Add($"Ngày {req.WorkDate:dd/MM} {shiftName}: Chưa có nhân viên kinh nghiệm (Level 2 trở lên).");
            }
        }

        // 2. Kiem tra phan cong cho tung nhan vien (BR02, BR05, BR06, BR07, BR08, BR09, BR10, BR11)
        var empAssignments = validAssignments
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var summaries = new List<EmployeeSummaryItemViewModel>();

        foreach (var emp in activeEmployees)
        {
            var userAssigns = empAssignments.TryGetValue(emp.EmployeeId, out var list) ? list : new List<ShiftAssignment>();
            var workDaysCount = userAssigns.Select(a => a.WorkDate).Distinct().Count();
            var totalShiftsCount = userAssigns.Count;
            var totalHours = totalShiftsCount * 4;

            // BR02: Chỉ nhân viên đang hoạt động
            if (!emp.IsActive && userAssigns.Any())
            {
                hardErrors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode}) đã ngưng hoạt động nhưng vẫn có ca.");
            }

            foreach (var assign in userAssigns)
            {
                var shiftName = shiftTypeMap.TryGetValue(assign.ShiftTypeId, out var st) ? st.ShiftName : $"Ca {assign.ShiftTypeId}";

                // BR05: Nhân viên chỉ được phân vào ca đã đăng ký rảnh
                if (!availMap.Contains(new Availability { EmployeeId = emp.EmployeeId, WorkDate = assign.WorkDate, ShiftTypeId = assign.ShiftTypeId }))
                {
                    hardErrors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode}) chưa đăng ký rảnh cho ngày {assign.WorkDate:dd/MM} {shiftName}.");
                }

                // BR06: Đơn xin nghỉ đã duyệt
                if (IsOnApprovedLeave(leaveSet, emp.EmployeeId, assign.WorkDate, assign.ShiftTypeId))
                {
                    hardErrors.Add($"Nhân viên {emp.FullName} ({emp.EmployeeCode}) có đơn nghỉ đã duyệt cho ngày {assign.WorkDate:dd/MM} {shiftName}.");
                }
            }

            // BR09 / BR10 / BR11 theo hình thức làm việc
            ValidateEmployeeWorkloadRules(emp, userAssigns, shiftTypes, hardErrors);

            // BR18 / BR19: Cảnh báo ưu tiên
            var warningText = GetWorkloadWarning(emp, totalShiftsCount, workDaysCount);

            summaries.Add(new EmployeeSummaryItemViewModel
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
            });
        }

        // BR19: Cán cân khối lượng công việc giữa các nhân viên cùng hình thức
        AddFairnessWarnings(activeEmployees, summaries, warnings);

        return new ScheduleValidationResultViewModel
        {
            IsValid = hardErrors.Count == 0,
            HardErrors = hardErrors.Distinct().ToList(),
            Warnings = warnings.Distinct().ToList(),
            EmployeeSummaries = summaries
        };
    }

    private static void ValidateEmployeeWorkloadRules(
        Employee emp,
        List<ShiftAssignment> userAssigns,
        IReadOnlyList<ShiftType> shiftTypes,
        List<string> hardErrors)
    {
        var shiftCodeMap = shiftTypes.ToDictionary(s => s.ShiftTypeId, s => s.ShiftCode);
        var dayGroups = userAssigns.GroupBy(a => a.WorkDate).ToList();

        if (emp.EmploymentType == EmploymentType.PartTime)
        {
            // BR09: Nhân viên bán thời gian chỉ được phân tối đa 1 ca/ngày
            foreach (var group in dayGroups)
            {
                if (group.Count() > 1)
                {
                    hardErrors.Add($"Nhân viên PT {emp.FullName} ({emp.EmployeeCode}) bị phân {group.Count()} ca trong ngày {group.Key:dd/MM}.");
                }
            }
        }
        else if (emp.EmploymentType == EmploymentType.FullTime)
        {
            // BR11: Nhân viên toàn thời gian không được phân quá 6 ngày/tuần
            if (dayGroups.Count > 6)
            {
                hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) làm {dayGroups.Count} ngày trong tuần (tối đa 6 ngày).");
            }

            // BR10: Nhân viên FT làm ngày nào phải đủ 2 ca liên tiếp (Sáng-Chiều hoặc Chiều-Tối)
            foreach (var group in dayGroups)
            {
                var codes = group.Select(a => shiftCodeMap.TryGetValue(a.ShiftTypeId, out var sc) ? sc : "").ToHashSet();
                var isMorningAfternoon = codes.Contains(ShiftTypeConstants.MorningCode) && codes.Contains(ShiftTypeConstants.AfternoonCode) && codes.Count == 2;
                var isAfternoonEvening = codes.Contains(ShiftTypeConstants.AfternoonCode) && codes.Contains(ShiftTypeConstants.EveningCode) && codes.Count == 2;

                if (!isMorningAfternoon && !isAfternoonEvening)
                {
                    hardErrors.Add($"Nhân viên FT {emp.FullName} ({emp.EmployeeCode}) ngày {group.Key:dd/MM} không đạt tổ hợp 2 ca liên tiếp (Sáng-Chiều hoặc Chiều-Tối).");
                }
            }
        }
    }

    private static bool IsOnApprovedLeave(List<LeaveRequest> leaveSet, int employeeId, DateOnly date, int shiftTypeId)
    {
        return leaveSet.Any(l => l.EmployeeId == employeeId
            && l.LeaveDate == date
            && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == shiftTypeId));
    }

    private static string GetWorkloadWarning(Employee emp, int totalShifts, int totalDays)
    {
        if (emp.EmploymentType == EmploymentType.PartTime)
        {
            if (totalShifts == 5) return "Đạt mức dự kiến (5 ca)";
            if (totalShifts < 5) return $"Chưa đủ 5 ca (mới có {totalShifts} ca)";
            return $"Vượt mức 5 ca ({totalShifts} ca)";
        }
        else
        {
            if (totalDays == 6) return "Đạt mức dự kiến (6 ngày / 48h)";
            if (totalDays < 6) return $"Chưa đủ 6 ngày làm việc (mới có {totalDays} ngày)";
            return $"Vượt mức 6 ngày ({totalDays} ngày)";
        }
    }

    private static void AddFairnessWarnings(
        IReadOnlyList<Employee> activeEmployees,
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
                warnings.Add($"Chênh lệch số ca làm việc giữa các nhân viên PT lớn ({minPt} ca đến {maxPt} ca).");
            }
        }

        var ftSummaries = summaries.Where(s => s.EmploymentType == EmploymentType.FullTime).ToList();
        if (ftSummaries.Count > 1)
        {
            var maxFt = ftSummaries.Max(s => s.TotalWorkDays);
            var minFt = ftSummaries.Min(s => s.TotalWorkDays);
            if (maxFt - minFt > 1)
            {
                warnings.Add($"Chênh lệch số ngày làm việc giữa các nhân viên FT lớn ({minFt} ngày đến {maxFt} ngày).");
            }
        }
    }

    private static bool IsFixedShiftCode(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode)) return false;
        return shiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AvailKeyComparer : IEqualityComparer<Availability>
    {
        public bool Equals(Availability? x, Availability? y)
        {
            if (x is null || y is null) return false;
            return x.EmployeeId == y.EmployeeId && x.WorkDate == y.WorkDate && x.ShiftTypeId == y.ShiftTypeId;
        }

        public int GetHashCode(Availability obj)
        {
            return HashCode.Combine(obj.EmployeeId, obj.WorkDate, obj.ShiftTypeId);
        }
    }
}
