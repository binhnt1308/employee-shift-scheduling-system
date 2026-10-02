using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public partial class WorkScheduleService
{
    /// <inheritdoc />
    public async Task<List<EligibleEmployeeOptionViewModel>> GetEligibleEmployeesForShiftAsync(
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null) return new List<EligibleEmployeeOptionViewModel>();

        // Tải dữ liệu liên quan theo lô một lần rồi đánh giá từng nhân viên trong bộ nhớ,
        // tránh N+1 khi dựng danh sách ứng viên.
        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, workDate, shiftTypeId, cancellationToken);
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, true, true, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);

        var currentAssignments = schedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>();
        var availSet = availabilities.Select(a => a.EmployeeId).ToHashSet();
        var leaveSet = leaves.Where(l => l.Status == LeaveRequestStatus.Approved).ToList();
        var shiftTypeMap = allShifts.ToDictionary(s => s.ShiftTypeId);
        var periodShifts = DailyShiftRuleEvaluator.ExtractPeriodShifts(period.StaffingRequirements, allShifts);
        var hasValidPolicy = DailyShiftRuleEvaluator.TryResolvePolicy(periodShifts, out var policy, out var policyError);
        var (policyMax, allowSplit) = hasValidPolicy ? policy : (1, false);

        var empMap = activeEmployees.ToDictionary(e => e.EmployeeId);
        var req = period.StaffingRequirements.FirstOrDefault(r =>
            r.WorkDate == workDate && r.ShiftTypeId == shiftTypeId);

        var currentShiftAssigns = currentAssignments
            .Where(a => a.WorkDate == workDate && a.ShiftTypeId == shiftTypeId)
            .ToList();

        var assignedSales = currentShiftAssigns.Count(a =>
            (empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Sales)
            || (a.Employee != null && a.Employee.Position == EmployeePosition.Sales));

        var assignedCashier = currentShiftAssigns.Count(a =>
            (empMap.TryGetValue(a.EmployeeId, out var e) && e.Position == EmployeePosition.Cashier)
            || (a.Employee != null && a.Employee.Position == EmployeePosition.Cashier));

        var result = activeEmployees.Select(emp =>
        {
            var option = BuildEligibleEmployeeOption(
                emp, workDate, shiftTypeId, period.WeekStartDate,
                availSet, leaveSet, currentAssignments, shiftTypeMap, policyMax, allowSplit,
                req, assignedSales, assignedCashier);

            if (!hasValidPolicy)
            {
                option.IsEligible = false;
                option.IneligibilityReason = policyError ?? "Cấu hình ca của kỳ lịch không hợp lệ.";
            }

            return option;
        }).ToList();

        return result.OrderByDescending(e => e.IsEligible)
                     .ThenBy(e => e.Position)
                     .ThenBy(e => e.FullName)
                     .ToList();
    }

    private static EligibleEmployeeOptionViewModel BuildEligibleEmployeeOption(
        Employee emp,
        DateOnly workDate,
        int shiftTypeId,
        DateOnly weekStartDate,
        HashSet<int> availSet,
        List<LeaveRequest> leaveSet,
        List<ShiftAssignment> currentAssignments,
        Dictionary<int, ShiftType> shiftTypeMap,
        int policyMax,
        bool allowSplit,
        StaffingRequirement? req,
        int assignedSales,
        int assignedCashier)
    {
        var hasAvail = availSet.Contains(emp.EmployeeId);
        var hasLeave = leaveSet.Any(l => l.EmployeeId == emp.EmployeeId && l.LeaveDate == workDate
            && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == shiftTypeId));
        var hasOverlap = currentAssignments.Any(a => a.EmployeeId == emp.EmployeeId
            && a.WorkDate == workDate && a.ShiftTypeId == shiftTypeId);

        var existingDayAssigns = currentAssignments
            .Where(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == workDate)
            .ToList();
        var existingDayShifts = existingDayAssigns
            .Select(a => shiftTypeMap.TryGetValue(a.ShiftTypeId, out var st) ? st : null)
            .Where(st => st != null)
            .Select(st => st!)
            .ToList();

        shiftTypeMap.TryGetValue(shiftTypeId, out var targetShift);

        // Gom mọi lý do không đủ điều kiện để giao diện giải thích đầy đủ thay vì chỉ trả lỗi đầu tiên.
        var reasons = new List<string>();
        if (!hasAvail) reasons.Add("Chưa đăng ký rảnh");
        if (hasLeave) reasons.Add("Có đơn nghỉ đã duyệt");
        if (hasOverlap) reasons.Add("Đã được phân vào ca này");

        if (req is null)
        {
            reasons.Add("Kỳ lịch không có dữ liệu định mức nhân sự cho ca này.");
        }
        else
        {
            if (emp.Position == EmployeePosition.Sales && assignedSales >= req.RequiredSalesStaff)
            {
                reasons.Add($"Ca này đã đủ định mức nhân viên Tư vấn ({req.RequiredSalesStaff}/{req.RequiredSalesStaff}).");
            }
            else if (emp.Position == EmployeePosition.Cashier && assignedCashier >= req.RequiredCashiers)
            {
                reasons.Add($"Ca này đã đủ định mức nhân viên Thu ngân ({req.RequiredCashiers}/{req.RequiredCashiers}).");
            }
        }

        var effectiveMax = DailyShiftRuleEvaluator.GetEffectiveMaxShifts(emp.EmploymentType, policyMax);
        if (!hasOverlap && existingDayShifts.Count >= effectiveMax)
        {
            reasons.Add(emp.EmploymentType == EmploymentType.PartTime
                ? "Đã đạt tối đa 1 ca/ngày"
                : $"Đã đạt tối đa {effectiveMax} ca/ngày");
        }

        var hasTimeOverlap = false;
        if (!hasOverlap && targetShift != null)
        {
            var candidateDayShifts = existingDayShifts.Append(targetShift).ToList();
            if (DailyShiftRuleEvaluator.HasAnyShiftOverlap(candidateDayShifts))
            {
                hasTimeOverlap = true;
                reasons.Add("Trùng hoặc chồng giờ làm việc");
            }
            if (emp.EmploymentType == EmploymentType.FullTime && DailyShiftRuleEvaluator.IsForbiddenSplitShift(candidateDayShifts, allowSplit))
            {
                reasons.Add("Không được phân ca gãy (Sáng-Tối)");
            }
        }

        var empAssignments = currentAssignments.Where(a => a.EmployeeId == emp.EmployeeId).ToList();
        empAssignments.Add(new ShiftAssignment
        {
            EmployeeId = emp.EmployeeId,
            WorkDate = workDate,
            ShiftTypeId = shiftTypeId
        });
        var restErrors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            emp, empAssignments, shiftTypeMap, weekStartDate);
        if (restErrors.Count > 0) reasons.Add("Vi phạm thời gian nghỉ tối thiểu (BR20/BR21)");

        var isEligible = hasAvail && !hasLeave && !hasOverlap && !hasTimeOverlap && reasons.Count == 0 && restErrors.Count == 0;
        return new EligibleEmployeeOptionViewModel
        {
            EmployeeId = emp.EmployeeId,
            EmployeeCode = emp.EmployeeCode,
            FullName = emp.FullName,
            Position = emp.Position,
            EmploymentType = emp.EmploymentType,
            Level = emp.Level,
            HasAvailability = hasAvail,
            HasApprovedLeave = hasLeave,
            HasOverlappingShift = hasOverlap || hasTimeOverlap,
            IsEligible = isEligible,
            IneligibilityReason = isEligible ? null : string.Join(", ", reasons)
        };
    }
}
