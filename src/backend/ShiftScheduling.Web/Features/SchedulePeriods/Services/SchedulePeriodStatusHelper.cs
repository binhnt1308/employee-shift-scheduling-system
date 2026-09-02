using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

public static class SchedulePeriodStatusHelper
{
    public sealed class EffectiveStatusResult
    {
        public string StatusName { get; init; } = string.Empty;
        public bool IsRegistrationOpen { get; init; }
        public bool NeedsAttention { get; init; }
        public string? AttentionMessage { get; init; }
        public string DisplayBadgeClass { get; init; } = "badge-ended";
    }

    public static EffectiveStatusResult CalculateEffectiveStatus(
        SchedulePeriod period,
        WorkSchedule? workSchedule,
        DateTime currentUtc,
        DateOnly currentDate)
    {
        // 1. Has Published WorkSchedule
        if (workSchedule is not null && workSchedule.Status == ScheduleStatus.Published)
        {
            if (currentDate <= period.WeekEndDate)
            {
                return new EffectiveStatusResult
                {
                    StatusName = "Đã công bố",
                    IsRegistrationOpen = false,
                    NeedsAttention = false,
                    DisplayBadgeClass = "badge-success"
                };
            }

            return new EffectiveStatusResult
            {
                StatusName = "Đã kết thúc",
                IsRegistrationOpen = false,
                NeedsAttention = false,
                DisplayBadgeClass = "badge-ended"
            };
        }

        // 2. Has WorkSchedule BUT NOT Published (e.g. Draft or Proposed)
        if (workSchedule is not null && workSchedule.Status != ScheduleStatus.Published)
        {
            var needsAttention = currentDate >= period.WeekStartDate;
            return new EffectiveStatusResult
            {
                StatusName = "Chưa công bố",
                IsRegistrationOpen = false,
                NeedsAttention = needsAttention,
                AttentionMessage = needsAttention ? "Kỳ lịch đã bắt đầu nhưng lịch làm việc chưa được công bố." : null,
                DisplayBadgeClass = needsAttention ? "badge-warning text-dark" : "badge-info-custom"
            };
        }

        // 3. NO WorkSchedule exists for this period
        if (currentDate > period.WeekEndDate)
        {
            return new EffectiveStatusResult
            {
                StatusName = "Đã kết thúc – chưa phân công",
                IsRegistrationOpen = false,
                NeedsAttention = false,
                DisplayBadgeClass = "badge-ended"
            };
        }

        if (currentDate >= period.WeekStartDate)
        {
            return new EffectiveStatusResult
            {
                StatusName = "Quá hạn – chưa phân công",
                IsRegistrationOpen = false,
                NeedsAttention = true,
                AttentionMessage = "Kỳ lịch đã bắt đầu nhưng chưa có lịch làm việc.",
                DisplayBadgeClass = "badge-rejected"
            };
        }

        if (currentUtc > period.AvailabilityDeadline)
        {
            return new EffectiveStatusResult
            {
                StatusName = "Chờ phân công",
                IsRegistrationOpen = false,
                NeedsAttention = false,
                DisplayBadgeClass = "badge-pending"
            };
        }

        if (period.RegistrationStatus == RegistrationStatus.Open)
        {
            var isRegistrationOpen = IsRegistrationEffectivelyOpen(period, currentUtc, currentDate);
            if (isRegistrationOpen)
            {
                return new EffectiveStatusResult
                {
                    StatusName = "Đang mở đăng ký",
                    IsRegistrationOpen = true,
                    NeedsAttention = false,
                    DisplayBadgeClass = "badge-active"
                };
            }

            return new EffectiveStatusResult
            {
                StatusName = "Chờ phân công",
                IsRegistrationOpen = false,
                NeedsAttention = false,
                DisplayBadgeClass = "badge-pending"
            };
        }

        if (period.RegistrationStatus == RegistrationStatus.Closed)
        {
            return new EffectiveStatusResult
            {
                StatusName = "Đã đóng đăng ký",
                IsRegistrationOpen = false,
                NeedsAttention = false,
                DisplayBadgeClass = "badge-ended"
            };
        }

        return new EffectiveStatusResult
        {
            StatusName = "Đang chuẩn bị",
            IsRegistrationOpen = false,
            NeedsAttention = false,
            DisplayBadgeClass = "badge-pending"
        };
    }

    /// <summary>
    /// Kiểm tra trạng thái đăng ký lịch rảnh có thực sự mở tại thời điểm hiện tại hay không.
    /// Điều kiện: RegistrationStatus == Open AND currentUtc <= AvailabilityDeadline AND currentDate < WeekStartDate.
    /// </summary>
    public static bool IsRegistrationEffectivelyOpen(
        SchedulePeriod period,
        DateTime currentUtc,
        DateOnly currentDate)
    {
        return period.RegistrationStatus == RegistrationStatus.Open
            && currentUtc <= period.AvailabilityDeadline
            && currentDate < period.WeekStartDate;
    }
}
