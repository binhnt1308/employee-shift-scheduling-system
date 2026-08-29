using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

public sealed partial class SchedulePeriodService
{
    /// <inheritdoc />
    public async Task<ServiceResult> OpenRegistrationAsync(
        int schedulePeriodId,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _schedulePeriodRepository.GetByIdAsync(
            schedulePeriodId, false, true, cancellationToken);
        if (period is null)
        {
            return ServiceResult.Failure(
                ErrorCodes.SchedulePeriodNotFound,
                "Không tìm thấy kỳ lịch.");
        }

        var statusResult = ValidateRegistrationPreconditions(period);
        if (!statusResult.Succeeded)
        {
            return statusResult;
        }

        var periodShifts = period.StaffingRequirements
            .Select(r => r.ShiftType)
            .Where(st => st != null)
            .DistinctBy(st => st.ShiftTypeId)
            .ToList();

        var validFixedShiftIds = periodShifts.Count == 3
            ? periodShifts.Select(st => st.ShiftTypeId).ToHashSet()
            : period.StaffingRequirements.Select(r => r.ShiftTypeId).ToHashSet();

        var validationResult = ValidateRegistrationRequirements(period, validFixedShiftIds);
        if (!validationResult.Succeeded)
        {
            return validationResult;
        }

        period.RegistrationStatus = RegistrationStatus.Open;

        await SendRegistrationOpenedNotificationsAsync(period, cancellationToken);

        LogAndAuditRegistrationOpen(period, actorAccountId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registration opened for schedule period. ActorAccountId: {ActorAccountId}; SchedulePeriodId: {PeriodId}",
            actorAccountId,
            schedulePeriodId);

        return ServiceResult.Success();
    }

    private async Task SendRegistrationOpenedNotificationsAsync(
        SchedulePeriod period,
        CancellationToken cancellationToken)
    {
        var employeeAccountIds = await _notificationRepository.GetActiveEmployeeAccountIdsAsync(cancellationToken);
        if (employeeAccountIds.Count == 0)
        {
            return;
        }

        var deadlineVietnam = _clock.ToVietnamTime(period.AvailabilityDeadline);
        var title = $"Mở đăng ký lịch rảnh: {period.PeriodName}";
        var startFormatted = period.WeekStartDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var endFormatted = period.WeekEndDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var deadlineFormatted = deadlineVietnam.ToString("HH:mm dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var content = $"Quản lý đã mở đăng ký lịch rảnh cho {period.PeriodName} ({startFormatted} - {endFormatted}). Hạn chót đăng ký: {deadlineFormatted}. Vui lòng đăng ký trước thời hạn.";
        var now = _clock.UtcNow;

        foreach (var accountId in employeeAccountIds)
        {
            _notificationRepository.Add(new Notification
            {
                ReceiverAccountId = accountId,
                Title = title,
                Content = content,
                Type = NotificationType.RegistrationOpened,
                RelatedObjectId = period.SchedulePeriodId,
                IsRead = false,
                CreatedAt = now
            });
        }
    }

    private ServiceResult ValidateRegistrationPreconditions(SchedulePeriod period)
    {
        if (period.RegistrationStatus != RegistrationStatus.Preparing)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidSchedulePeriodStatus,
                "Kỳ lịch không ở trạng thái Chuẩn bị.");
        }

        // BR04: Kỳ lịch chỉ mở đăng ký khi thời hạn đăng ký còn hiệu lực.
        if (period.AvailabilityDeadline <= _clock.UtcNow)
        {
            return ServiceResult.Failure(
                ErrorCodes.RegistrationDeadlineExpired,
                "Thời hạn đăng ký lịch rảnh đã hết. Không thể mở đăng ký.");
        }

        return ServiceResult.Success();
    }

    private void LogAndAuditRegistrationOpen(SchedulePeriod period, int actorAccountId)
    {
        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "OPEN_PERIOD_REGISTRATION",
            nameof(SchedulePeriod),
            period.SchedulePeriodId.ToString(),
            new { RegistrationStatus = RegistrationStatus.Preparing },
            new { RegistrationStatus = RegistrationStatus.Open },
            _clock.UtcNow));
    }

    private static ServiceResult ValidateRegistrationRequirements(
        SchedulePeriod period,
        HashSet<int> validFixedShiftIds)
    {
        // BR03: Hệ thống chỉ sử dụng ba ca cố định trong kỳ lịch.
        // Revalidate exact 21 combinations (7 days x 3 shifts).
        var requirements = period.StaffingRequirements.ToList();
        var validFixedRequirements = requirements.Where(r => validFixedShiftIds.Contains(r.ShiftTypeId)).ToList();

        if (validFixedRequirements.Count != 21 || requirements.Count != 21)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidStaffingRequirement,
                "Kỳ lịch phải có chính xác đúng 21 định mức ca (7 ngày x 3 ca cố định).");
        }

        var combinations = BuildExpectedCombinations(period.WeekStartDate, validFixedShiftIds);

        foreach (var req in requirements)
        {
            var reqValidation = ValidateSingleRequirementForRegistration(period, req, combinations);
            if (!reqValidation.Succeeded)
            {
                return reqValidation;
            }
        }

        if (combinations.Count > 0)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidStaffingRequirement,
                "Thiếu định mức cho một số ca trong 21 ca cố định.");
        }

        return ServiceResult.Success();
    }

    private static ServiceResult ValidateSingleRequirementForRegistration(
        SchedulePeriod period,
        StaffingRequirement req,
        HashSet<(DateOnly WorkDate, int ShiftTypeId)> combinations)
    {
        if (req.WorkDate < period.WeekStartDate || req.WorkDate > period.WeekEndDate)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidDateRange,
                $"Ngày làm việc {req.WorkDate:dd/MM/yyyy} nằm ngoài kỳ lịch.");
        }

        if (!combinations.Remove((req.WorkDate, req.ShiftTypeId)))
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidStaffingRequirement,
                "Định mức ca bị trùng lặp hoặc chứa ca thứ 4 không hợp lệ.");
        }

        // BR12: Mỗi ca phải có định mức nhân sự hợp lệ theo từng vị trí.
        if (req.RequiredSalesStaff < 0
            || req.RequiredCashiers < 0
            || (req.RequiredSalesStaff + req.RequiredCashiers) <= 0)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidStaffingRequirement,
                $"Định mức nhân sự cho ngày {req.WorkDate:dd/MM/yyyy} chưa hợp lệ " +
                "(tổng Bán hàng + Thu ngân phải lớn hơn 0).");
        }

        // BR13: Mỗi ca phải yêu cầu ít nhất một nhân viên từ Level 2 trở lên.
        if (req.RequiredExperiencedStaff < 1 || req.MinimumLevel < EmployeeLevel.Level2)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidStaffingRequirement,
                "Mỗi ca làm việc phải yêu cầu ít nhất 1 nhân viên kinh nghiệm"
                    + " (từ Level 2 trở lên).");
        }

        return ServiceResult.Success();
    }

    private static HashSet<(DateOnly WorkDate, int ShiftTypeId)> BuildExpectedCombinations(
        DateOnly weekStartDate,
        HashSet<int> validFixedShiftIds)
    {
        var combinations = new HashSet<(DateOnly WorkDate, int ShiftTypeId)>();
        for (var day = 0; day < 7; day++)
        {
            var date = weekStartDate.AddDays(day);
            foreach (var shiftId in validFixedShiftIds)
            {
                combinations.Add((date, shiftId));
            }
        }
        return combinations;
    }
}
