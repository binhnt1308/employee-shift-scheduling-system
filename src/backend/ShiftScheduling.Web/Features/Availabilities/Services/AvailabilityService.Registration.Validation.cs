using System.Globalization;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Availabilities.Services;

public sealed partial class AvailabilityService
{
    private sealed class PeriodValidationResult
    {
        public bool HasError { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public SchedulePeriod? Period { get; init; }
    }

    private async Task<PeriodValidationResult> ValidatePeriodForRegistrationAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken)
    {
        var period = await _periodRepository.GetByIdAsync(
            schedulePeriodId,
            true,
            true,
            cancellationToken);

        if (period is null)
        {
            return PeriodValidationFailure(
                ErrorCodes.SchedulePeriodNotFound,
                "Không tìm thấy kỳ lịch.");
        }

        if (period.RegistrationStatus != RegistrationStatus.Open)
        {
            return PeriodValidationFailure(
                ErrorCodes.InvalidSchedulePeriodStatus,
                "Kỳ lịch chưa mở đăng ký hoặc đã đóng đăng ký.");
        }

        var now = _clock.UtcNow;
        if (now > period.AvailabilityDeadline)
        {
            return PeriodValidationFailure(
                ErrorCodes.RegistrationDeadlineExpired,
                "Đã hết thời hạn đăng ký lịch rảnh.");
        }

        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));
        if (currentDate >= period.WeekStartDate)
        {
            return PeriodValidationFailure(
                ErrorCodes.InvalidSchedulePeriodStatus,
                "Kỳ lịch đã bắt đầu, không thể đăng ký thêm lịch rảnh.");
        }

        return new PeriodValidationResult
        {
            Period = period
        };
    }

    private static PeriodValidationResult PeriodValidationFailure(
        string errorCode,
        string errorMessage)
    {
        return new PeriodValidationResult
        {
            HasError = true,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    private static HashSet<int> ResolveValidShiftIds(
        SchedulePeriod period,
        IReadOnlyList<ShiftType> activeShifts)
    {
        var periodShiftIds = period.StaffingRequirements
            .Select(requirement => requirement.ShiftTypeId)
            .ToHashSet();

        return periodShiftIds.Count > 0
            ? periodShiftIds
            : activeShifts.Select(shift => shift.ShiftTypeId).ToHashSet();
    }

    private sealed class AvailabilitySelectionValidationResult
    {
        public bool HasError { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public List<(DateOnly WorkDate, int ShiftTypeId)> ParsedShifts { get; init; } = new();

        public Dictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel>
            PreferenceMap
        { get; init; } = new();
    }

    /// <summary>
    /// Kiểm tra toàn bộ danh sách ca rảnh theo hai bước: xác thực khóa ca thuộc kỳ lịch,
    /// sau đó chỉ chấp nhận mức nguyện vọng gắn với những ca đã được chọn.
    /// </summary>
    private async Task<AvailabilitySelectionValidationResult> ValidateAvailabilitySelectionAsync(
        SchedulePeriod period,
        List<string> selectedShiftKeys,
        Dictionary<string, AvailabilityPreferenceLevel> shiftPreferences,
        CancellationToken cancellationToken)
    {
        var activeShifts = await _shiftTypeRepository.GetActiveShiftsAsync(true, cancellationToken);
        var validShiftIds = ResolveValidShiftIds(period, activeShifts);

        var parseResult = ParseAndValidateSelectedShiftKeys(
            selectedShiftKeys,
            period,
            validShiftIds);
        if (parseResult.HasError)
        {
            return AvailabilitySelectionFailure(
                parseResult.ErrorCode!,
                parseResult.ErrorMessage!);
        }

        var preferenceResult = ValidateAndBuildPreferenceMap(
            parseResult.ParsedShifts,
            parseResult.SelectedShiftKeySet,
            shiftPreferences);
        if (preferenceResult.HasError)
        {
            return AvailabilitySelectionFailure(
                preferenceResult.ErrorCode!,
                preferenceResult.ErrorMessage!);
        }

        return new AvailabilitySelectionValidationResult
        {
            ParsedShifts = parseResult.ParsedShifts,
            PreferenceMap = preferenceResult.PreferenceMap
        };
    }

    private static AvailabilitySelectionValidationResult AvailabilitySelectionFailure(
        string errorCode,
        string errorMessage)
    {
        return new AvailabilitySelectionValidationResult
        {
            HasError = true,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    private sealed class SelectedShiftKeyParseResult
    {
        public bool HasError { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public List<(DateOnly WorkDate, int ShiftTypeId)> ParsedShifts { get; init; } = new();
        public HashSet<string> SelectedShiftKeySet { get; init; } = new();
    }

    private static SelectedShiftKeyParseResult ParseAndValidateSelectedShiftKeys(
        List<string> selectedShiftKeys,
        SchedulePeriod period,
        HashSet<int> validShiftIds)
    {
        var selectedShiftKeySet = new HashSet<string>();
        var parsedShifts = new List<(DateOnly WorkDate, int ShiftTypeId)>();

        // Phân tích khóa một lần và dùng HashSet để từ chối khóa trùng trước khi truy cập repository.
        foreach (var key in selectedShiftKeys ?? new List<string>())
        {
            if (!selectedShiftKeySet.Add(key))
            {
                return SelectedShiftKeyFailure($"Ca rảnh bị chọn trùng lặp: {key}.");
            }

            if (!TryParseShiftKey(key, out var date, out var shiftTypeId))
            {
                return SelectedShiftKeyFailure($"Mã ca rảnh không đúng định dạng: {key}.");
            }

            var isOutsidePeriod = date < period.WeekStartDate || date > period.WeekEndDate;
            if (isOutsidePeriod || !validShiftIds.Contains(shiftTypeId))
            {
                return SelectedShiftKeyFailure(
                    $"Ca rảnh nằm ngoài kỳ lịch hoặc ca không hoạt động: {key}.");
            }

            parsedShifts.Add((date, shiftTypeId));
        }

        return new SelectedShiftKeyParseResult
        {
            ParsedShifts = parsedShifts,
            SelectedShiftKeySet = selectedShiftKeySet
        };
    }

    private static SelectedShiftKeyParseResult SelectedShiftKeyFailure(string errorMessage)
    {
        return new SelectedShiftKeyParseResult
        {
            HasError = true,
            ErrorCode = ErrorCodes.InvalidShiftSelection,
            ErrorMessage = errorMessage
        };
    }

    private sealed class PreferenceMapValidationResult
    {
        public bool HasError { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }

        public Dictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel>
            PreferenceMap
        { get; init; } = new();
    }

    private static PreferenceMapValidationResult ValidateAndBuildPreferenceMap(
        List<(DateOnly WorkDate, int ShiftTypeId)> parsedShifts,
        HashSet<string> selectedShiftKeySet,
        Dictionary<string, AvailabilityPreferenceLevel> shiftPreferences)
    {
        var preferenceMap =
            new Dictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel>();

        // Client không được gửi preference cho một ca chưa chọn; server luôn kiểm tra lại thay vì tin dữ liệu form.
        foreach (var preference in shiftPreferences)
        {
            if (!selectedShiftKeySet.Contains(preference.Key))
            {
                return PreferenceMapFailure(
                    ErrorCodes.InvalidShiftSelection,
                    $"Mức nguyện vọng được gửi cho ca rảnh không được chọn: {preference.Key}.");
            }

            if (!TryParseShiftKey(preference.Key, out var date, out var shiftTypeId))
            {
                return PreferenceMapFailure(
                    ErrorCodes.InvalidShiftSelection,
                    $"Key nguyện vọng không đúng định dạng: {preference.Key}.");
            }

            if (!Enum.IsDefined(preference.Value))
            {
                return PreferenceMapFailure(
                    ErrorCodes.InvalidEnumValue,
                    $"Mức nguyện vọng '{(int)preference.Value}' không hợp lệ.");
            }

            preferenceMap[(date, shiftTypeId)] = preference.Value;
        }

        // Ca được chọn nhưng không chỉ định preference được chuẩn hóa về Normal để dữ liệu lưu luôn đầy đủ.
        foreach (var parsedShift in parsedShifts)
        {
            if (!preferenceMap.ContainsKey(parsedShift))
            {
                preferenceMap[parsedShift] = AvailabilityPreferenceLevel.Normal;
            }
        }

        return new PreferenceMapValidationResult
        {
            PreferenceMap = preferenceMap
        };
    }

    private static PreferenceMapValidationResult PreferenceMapFailure(
        string errorCode,
        string errorMessage)
    {
        return new PreferenceMapValidationResult
        {
            HasError = true,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    private static bool TryParseShiftKey(
        string key,
        out DateOnly date,
        out int shiftTypeId)
    {
        date = default;
        shiftTypeId = default;

        var parts = key.Split('_');
        if (parts.Length != 2)
        {
            return false;
        }

        return DateOnly.TryParseExact(
                parts[0],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date)
            && int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out shiftTypeId);
    }
}
