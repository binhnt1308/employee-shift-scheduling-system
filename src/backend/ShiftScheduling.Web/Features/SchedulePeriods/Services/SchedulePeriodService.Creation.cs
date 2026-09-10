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
    public async Task<ServiceResult> CreateAsync(
        SchedulePeriodCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        if (model.WeekStartDate == default)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidDateRange,
                "Ngày bắt đầu tuần không hợp lệ.");
        }

        // BR03: Mỗi kỳ lịch tương ứng với đúng bảy ngày làm việc (7 ngày).
        var weekStartDate = model.WeekStartDate;
        var weekEndDate = weekStartDate.AddDays(6);
        var availabilityDeadlineUtc = _clock.ToUtcFromVietnamTime(model.AvailabilityDeadline);

        if (availabilityDeadlineUtc <= _clock.UtcNow)
        {
            return ServiceResult.Failure(
            ErrorCodes.InvalidDateRange,
            "Hạn đăng ký lịch rảnh phải ở tương lai.");
        }

        if (await _schedulePeriodRepository.HasOverlappingPeriodAsync(
            weekStartDate, weekEndDate, null, cancellationToken))
        {
            return ServiceResult.Failure(
                ErrorCodes.SchedulePeriodOverlap,
                "Kỳ lịch trùng thời gian với kỳ lịch đã có.");
        }

        var activeShiftTypes = await _shiftTypeRepository.GetActiveShiftsAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(activeShiftTypes);

        // BR03: Hệ thống phải có và chỉ sử dụng đúng ba ca cố định (Morning, Afternoon, Evening)
        if (fixedShifts.Count != 3 || fixedShifts.Any(s => s.EndTime <= s.StartTime))
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftTypeNotFound,
                "Hệ thống phải có đủ 3 ca cố định đang hoạt động hợp lệ: Ca sáng (MORNING), Ca chiều (AFTERNOON) và Ca tối (EVENING).");
        }

        var version = fixedShifts[0].ConfigurationVersion;
        if (fixedShifts.Any(s => s.ConfigurationVersion != version))
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftConfigurationInvalid,
                "Các ca làm việc đang hoạt động không cùng một phiên bản cấu hình.");
        }

        var period = CreateSchedulePeriodEntity(
            model, weekStartDate, weekEndDate, availabilityDeadlineUtc, actorAccountId);
        _schedulePeriodRepository.Add(period);

        // BR03: Hệ thống chỉ sử dụng ba ca cố định trong kỳ lịch.
        // Tạo đúng 21 combinations (7 ngày x 3 ca cố định).
        AddFixedStaffingRequirements(period, weekStartDate, fixedShifts);

        LogAndAuditCreate(period.PeriodName, weekStartDate, weekEndDate, availabilityDeadlineUtc, actorAccountId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Schedule period created. ActorAccountId: {ActorAccountId}; WeekStartDate: {WeekStartDate}",
            actorAccountId,
            weekStartDate);

        return ServiceResult.Success();
    }

    private SchedulePeriod CreateSchedulePeriodEntity(
        SchedulePeriodCreateViewModel model,
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        DateTime availabilityDeadlineUtc,
        int actorAccountId)
    {
        var periodName = string.IsNullOrWhiteSpace(model.PeriodName)
            ? $"Kỳ lịch {weekStartDate:dd/MM/yyyy} - {weekEndDate:dd/MM/yyyy}"
            : model.PeriodName.Trim();

        return new SchedulePeriod
        {
            PeriodName = periodName,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            AvailabilityDeadline = availabilityDeadlineUtc,
            RegistrationStatus = RegistrationStatus.Preparing,
            CreatedByAccountId = actorAccountId,
            CreatedAt = _clock.UtcNow
        };
    }

    private void AddFixedStaffingRequirements(
        SchedulePeriod period,
        DateOnly weekStartDate,
        List<ShiftType> fixedShifts)
    {
        // BR03: Hệ thống chỉ sử dụng ba ca cố định trong kỳ lịch.
        // Tạo đúng 21 combinations (7 ngày x 3 ca cố định).
        for (var day = 0; day < 7; day++)
        {
            var workDate = weekStartDate.AddDays(day);
            foreach (var shift in fixedShifts.OrderBy(st => st.DisplayOrder))
            {
                // BR13: Mỗi ca phải có ít nhất một nhân viên Level 2 trở lên.
                var requirement = new StaffingRequirement
                {
                    SchedulePeriod = period,
                    WorkDate = workDate,
                    ShiftTypeId = shift.ShiftTypeId,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                };
                _schedulePeriodRepository.AddRequirement(requirement);
            }
        }
    }

    private void LogAndAuditCreate(
        string periodName,
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        DateTime availabilityDeadline,
        int actorAccountId)
    {
        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "CREATE_SCHEDULE_PERIOD",
            nameof(SchedulePeriod),
            periodName,
            null,
            new { weekStartDate, weekEndDate, availabilityDeadline },
            _clock.UtcNow));
    }
}
