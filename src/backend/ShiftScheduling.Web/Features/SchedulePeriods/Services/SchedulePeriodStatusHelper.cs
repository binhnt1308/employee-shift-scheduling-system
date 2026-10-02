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
        public bool CanClose { get; init; }
    }

    /// <summary>
    /// Tính trạng thái hiển thị từ trạng thái đăng ký, WorkSchedule và mốc thời gian hiện tại.
    /// Đây là nguồn dùng chung để các màn hình không tự suy diễn trạng thái khác nhau.
    /// </summary>
    public static EffectiveStatusResult CalculateEffectiveStatus(
        SchedulePeriod period,
        WorkSchedule? workSchedule,
        DateTime currentUtc,
        DateOnly currentDate)
    {
        // QT-6: Khi lịch làm việc đã chốt, ngày công bị khóa hoàn toàn.
        if (workSchedule is not null && workSchedule.Status == ScheduleStatus.Closed)
        {
            return new EffectiveStatusResult
            {
                StatusName = "Đã chốt",
                IsRegistrationOpen = false,
                NeedsAttention = false,
                DisplayBadgeClass = "badge-ended"
            };
        }

        // QT-6 và BRD 5.4: Lịch đã công bố chuyển từ "Đã công bố" sang "Đang áp dụng" từ ngày bắt đầu kỳ
        // và giữ "Đang áp dụng" cho đến khi Quản lý thực hiện chốt kỳ.
        if (workSchedule is not null && workSchedule.Status == ScheduleStatus.Published)
        {
            if (currentDate < period.WeekStartDate)
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
                StatusName = "Đang áp dụng",
                IsRegistrationOpen = false,
                NeedsAttention = false,
                DisplayBadgeClass = "badge-active"
            };
        }

        // Lịch nháp hiển thị trạng thái Bản nháp theo BRD 5.4.
        if (workSchedule is not null)
        {
            var needsAttention = currentDate >= period.WeekStartDate;
            return new EffectiveStatusResult
            {
                StatusName = "Bản nháp",
                IsRegistrationOpen = false,
                NeedsAttention = needsAttention,
                AttentionMessage = needsAttention ? "Kỳ lịch đã bắt đầu nhưng lịch làm việc chưa được công bố." : null,
                DisplayBadgeClass = needsAttention ? "badge-warning text-dark" : "badge-info-custom"
            };
        }

        // 3. Kỳ lịch chưa có WorkSchedule nên trạng thái phụ thuộc thời gian đăng ký.
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
    /// Đăng ký chỉ thực sự mở khi status là Open, chưa quá hạn và kỳ lịch chưa bắt đầu.
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

    /// <summary>
    /// Kiểm tra xem lịch làm việc của kỳ đã đủ điều kiện để chốt hay chưa (QT-6).
    /// Điều kiện: Lịch ở trạng thái Published và ngày hiện tại đã qua WeekEndDate.
    /// </summary>
    public static bool CanCloseSchedule(
        SchedulePeriod period,
        WorkSchedule? workSchedule,
        DateOnly currentDate)
    {
        return workSchedule is not null
            && workSchedule.Status == ScheduleStatus.Published
            && currentDate > period.WeekEndDate;
    }
}
