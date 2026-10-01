using System.Globalization;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Services;

public sealed partial class AvailabilityService
{
    private sealed record RegistrationState(
        bool IsEditable,
        string? ReadonlyReason);

    private static Dictionary<string, Availability> BuildExistingAvailabilityMap(
        IEnumerable<Availability> existing)
    {
        return existing.ToDictionary(
            availability =>
                $"{availability.WorkDate:yyyy-MM-dd}_{availability.ShiftTypeId}");
    }

    private RegistrationState ResolveRegistrationState(SchedulePeriod period)
    {
        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));

        if (period.RegistrationStatus != RegistrationStatus.Open)
        {
            return new RegistrationState(
                false,
                "Kỳ lịch chưa mở đăng ký hoặc đã đóng đăng ký.");
        }

        if (now > period.AvailabilityDeadline)
        {
            return new RegistrationState(
                false,
                "Thời hạn đăng ký lịch rảnh đã hết.");
        }

        if (currentDate >= period.WeekStartDate)
        {
            return new RegistrationState(
                false,
                "Kỳ lịch đã bắt đầu, không thể đăng ký thêm lịch rảnh.");
        }

        return new RegistrationState(true, null);
    }

    private List<AvailabilityDayViewModel> BuildDayViewModels(
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        Dictionary<string, Availability> existingMap)
    {
        var viCulture = new CultureInfo("vi-VN");
        var days = new List<AvailabilityDayViewModel>();

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = period.WeekStartDate.AddDays(dayOffset);
            var dayName = date.ToString("dddd (dd/MM)", viCulture);

            var dayShifts = fixedShifts
                .Select(shift => BuildAvailabilityCell(date, shift, existingMap))
                .ToList();

            days.Add(new AvailabilityDayViewModel
            {
                WorkDate = date,
                DayOfWeekName = dayName,
                Shifts = dayShifts
            });
        }

        return days;
    }

    private static AvailabilityCellViewModel BuildAvailabilityCell(
        DateOnly date,
        ShiftType shift,
        IReadOnlyDictionary<string, Availability> existingMap)
    {
        var key = $"{date:yyyy-MM-dd}_{shift.ShiftTypeId}";
        var existingAvailability =
            existingMap.TryGetValue(key, out var availability) ? availability : null;

        return new AvailabilityCellViewModel
        {
            WorkDate = date,
            ShiftTypeId = shift.ShiftTypeId,
            ShiftCode = shift.ShiftCode,
            ShiftName = shift.ShiftName,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            IsSelected = existingAvailability is not null,
            PreferenceLevel = existingAvailability?.PreferenceLevel
                ?? AvailabilityPreferenceLevel.Normal
        };
    }
}
