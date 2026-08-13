using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

public sealed partial class SchedulePeriodService
{
    /// <inheritdoc />
    public async Task<ServiceResult> UpdateStaffingAsync(
        StaffingRequirementEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _schedulePeriodRepository.GetByIdAsync(
            model.SchedulePeriodId, false, true, cancellationToken);
        if (period is null)
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy kỳ lịch.");
        }

        if (period.RegistrationStatus != RegistrationStatus.Preparing)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidSchedulePeriodStatus,
                "Chỉ được chỉnh sửa định mức khi kỳ lịch ở trạng thái Chuẩn bị.");
        }

        var allShiftTypes = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(allShiftTypes);
        var validFixedShiftIds = fixedShifts.Select(st => st.ShiftTypeId).ToHashSet();

        var validationResult = ValidateStaffingItems(period, model.Requirements, validFixedShiftIds);
        if (!validationResult.Succeeded)
        {
            return validationResult;
        }

        ApplyStaffingRequirementUpdates(period, model.Requirements);

        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "UPDATE_STAFFING_REQUIREMENTS",
            nameof(SchedulePeriod),
            period.SchedulePeriodId.ToString(),
            null,
            new { period.SchedulePeriodId, ItemCount = model.Requirements.Count },
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Staffing requirements updated. ActorAccountId: {ActorAccountId}; SchedulePeriodId: {SchedulePeriodId}",
            actorAccountId,
            period.SchedulePeriodId);

        return ServiceResult.Success();
    }

    private static ServiceResult ValidateStaffingItems(
        SchedulePeriod period,
        List<StaffingRequirementItemViewModel> items,
        HashSet<int> validFixedShiftIds)
    {
        foreach (var item in items)
        {
            if (item.WorkDate < period.WeekStartDate || item.WorkDate > period.WeekEndDate)
            {
                return ServiceResult.Failure(
                    ErrorCodes.InvalidDateRange,
                    $"Ngày làm việc {item.WorkDate:dd/MM/yyyy} nằm ngoài kỳ lịch.");
            }

            // BR03: Hệ thống chỉ sử dụng ba ca cố định trong kỳ lịch. Từ chối ShiftType ngoài 3 ca cố định.
            if (!validFixedShiftIds.Contains(item.ShiftTypeId))
            {
                return ServiceResult.Failure(
                    ErrorCodes.ShiftTypeNotFound,
                    "Ca làm việc không thuộc 3 ca cố định của kỳ lịch.");
            }

            // BR12: Mỗi ca phải có định mức nhân sự hợp lệ theo từng vị trí.
            if (item.RequiredSalesStaff < 0)
            {
                return ServiceResult.Failure(
                    ErrorCodes.InvalidStaffingRequirement,
                    "Số lượng nhân viên bán hàng không được âm.");
            }

            if (item.RequiredCashiers < 0)
            {
                return ServiceResult.Failure(
                    ErrorCodes.InvalidStaffingRequirement,
                    "Số lượng nhân viên thu ngân không được âm.");
            }

            if (item.RequiredSalesStaff + item.RequiredCashiers <= 0)
            {
                return ServiceResult.Failure(
                    ErrorCodes.InvalidStaffingRequirement,
                    $"Tổng nhân sự cho ngày {item.WorkDate:dd/MM/yyyy} ({item.ShiftName}) phải lớn hơn 0.");
            }
        }

        return ServiceResult.Success();
    }

    private static void ApplyStaffingRequirementUpdates(
        SchedulePeriod period,
        List<StaffingRequirementItemViewModel> items)
    {
        foreach (var item in items)
        {
            var req = period.StaffingRequirements.FirstOrDefault(r =>
                r.StaffingRequirementId == item.StaffingRequirementId
                || (r.WorkDate == item.WorkDate && r.ShiftTypeId == item.ShiftTypeId));

            if (req is not null)
            {
                req.RequiredSalesStaff = item.RequiredSalesStaff;
                req.RequiredCashiers = item.RequiredCashiers;
                // BR13: Mỗi ca phải yêu cầu ít nhất một nhân viên từ Level 2 trở lên.
                req.RequiredExperiencedStaff = 1;
                req.MinimumLevel = EmployeeLevel.Level2;
            }
        }
    }
}
