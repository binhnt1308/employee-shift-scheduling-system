using System.Globalization;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Availabilities.Services;

public sealed partial class AvailabilityService
{
    /// <inheritdoc />
    public async Task<List<AvailabilityPeriodItemViewModel>> GetEmployeePeriodsAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return new List<AvailabilityPeriodItemViewModel>();
        }

        var periodsResult = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));

        var result = new List<AvailabilityPeriodItemViewModel>();
        foreach (var period in periodsResult.Items)
        {
            var avails = await _availabilityRepository.GetByEmployeeAndPeriodAsync(
                employee.EmployeeId, period.SchedulePeriodId, cancellationToken);

            var isOpen = period.RegistrationStatus == RegistrationStatus.Open;
            var isExpired = now > period.AvailabilityDeadline || currentDate >= period.WeekStartDate;

            result.Add(new AvailabilityPeriodItemViewModel
            {
                SchedulePeriodId = period.SchedulePeriodId,
                PeriodName = period.PeriodName,
                WeekStartDate = period.WeekStartDate,
                WeekEndDate = period.WeekEndDate,
                AvailabilityDeadline = _clock.ToVietnamTime(period.AvailabilityDeadline),
                RegistrationStatus = period.RegistrationStatus,
                IsOpen = isOpen && !isExpired,
                IsExpired = isExpired,
                RegisteredCount = avails.Count
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AvailabilityRegisterViewModel?> GetEmployeeRegisterFormAsync(
        int accountId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var fixedShifts = await ResolveFixedShiftsAsync(period, cancellationToken);

        var existing = await _availabilityRepository.GetByEmployeeAndPeriodAsync(
            employee.EmployeeId, schedulePeriodId, cancellationToken);

        var existingMap = existing.ToDictionary(
            a => $"{a.WorkDate:yyyy-MM-dd}_{a.ShiftTypeId}");

        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));
        var isOpen = period.RegistrationStatus == RegistrationStatus.Open;
        var isDeadlineValid = now <= period.AvailabilityDeadline;
        var isBeforeStart = currentDate < period.WeekStartDate;
        var isEditable = isOpen && isDeadlineValid && isBeforeStart;

        string? readonlyReason = null;
        if (!isOpen)
        {
            readonlyReason = "Kỳ lịch chưa mở đăng ký hoặc đã đóng đăng ký.";
        }
        else if (!isDeadlineValid)
        {
            readonlyReason = "Thời hạn đăng ký lịch rảnh đã hết.";
        }
        else if (!isBeforeStart)
        {
            readonlyReason = "Kỳ lịch đã bắt đầu, không thể đăng ký thêm lịch rảnh.";
        }

        var days = BuildDayViewModels(period, fixedShifts, existingMap);

        var weeklyPatterns = await _weeklyPatternRepository.GetByEmployeeIdAsync(
            employee.EmployeeId, cancellationToken);

        var weeklyPatternViewModel = BuildWeeklyPatternViewModel(weeklyPatterns, fixedShifts);

        return new AvailabilityRegisterViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            AvailabilityDeadline = _clock.ToVietnamTime(period.AvailabilityDeadline),
            IsEditable = isEditable,
            ReadonlyReason = readonlyReason,
            Days = days,
            SelectedShiftKeys = existingMap.Keys.ToList(),
            WeeklyPattern = weeklyPatternViewModel
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SaveEmployeeAvailabilityAsync(
        int accountId,
        int schedulePeriodId,
        List<string> selectedShiftKeys,
        Dictionary<string, AvailabilityPreferenceLevel>? shiftPreferences = null,
        CancellationToken cancellationToken = default)
    {
        shiftPreferences ??= new Dictionary<string, AvailabilityPreferenceLevel>();
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null || !employee.IsActive)
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotFound, "Không tìm thấy hồ sơ nhân viên hoặc tài khoản không hoạt động.");
        }

        var periodValidation = await ValidatePeriodForRegistrationAsync(schedulePeriodId, cancellationToken);
        if (periodValidation.HasError)
        {
            return ServiceResult.Failure(periodValidation.ErrorCode!, periodValidation.ErrorMessage!);
        }

        var period = periodValidation.Period!;
        var validShiftIds = ResolveValidShiftIds(period,
            await _shiftTypeRepository.GetActiveShiftsAsync(true, cancellationToken));

        var parseResult = ParseAndValidateSelectedShiftKeys(selectedShiftKeys, period, validShiftIds);
        if (parseResult.HasError)
        {
            return ServiceResult.Failure(parseResult.ErrorCode!, parseResult.ErrorMessage!);
        }

        var prefResult = ValidateAndBuildPreferenceMap(
            parseResult.ParsedShifts, parseResult.SelectedShiftKeySet, shiftPreferences);
        if (prefResult.HasError)
        {
            return ServiceResult.Failure(prefResult.ErrorCode!, prefResult.ErrorMessage!);
        }

        var now = _clock.UtcNow;
        await _availabilityRepository.SaveEmployeeAvailabilitiesAsync(
            employee.EmployeeId, schedulePeriodId, parseResult.ParsedShifts, prefResult.PreferenceMap, now, cancellationToken);

        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "SAVE_EMPLOYEE_AVAILABILITY",
            nameof(Availability),
            $"{employee.EmployeeCode}_{period.SchedulePeriodId}",
            null,
            new { schedulePeriodId, Count = parseResult.ParsedShifts.Count },
            now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee availability saved. EmployeeCode: {EmployeeCode}; PeriodId: {PeriodId}; Count: {Count}",
            employee.EmployeeCode, schedulePeriodId, parseResult.ParsedShifts.Count);

        return ServiceResult.Success();
    }

    // ── private helpers ───────────────────────────────────────────────

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
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return new PeriodValidationResult { HasError = true, ErrorCode = ErrorCodes.SchedulePeriodNotFound, ErrorMessage = "Không tìm thấy kỳ lịch." };
        }

        if (period.RegistrationStatus != RegistrationStatus.Open)
        {
            return new PeriodValidationResult { HasError = true, ErrorCode = ErrorCodes.InvalidSchedulePeriodStatus, ErrorMessage = "Kỳ lịch chưa mở đăng ký hoặc đã đóng đăng ký." };
        }

        var now = _clock.UtcNow;
        if (now > period.AvailabilityDeadline)
        {
            return new PeriodValidationResult { HasError = true, ErrorCode = ErrorCodes.RegistrationDeadlineExpired, ErrorMessage = "Đã hết thời hạn đăng ký lịch rảnh." };
        }

        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));
        if (currentDate >= period.WeekStartDate)
        {
            return new PeriodValidationResult { HasError = true, ErrorCode = ErrorCodes.InvalidSchedulePeriodStatus, ErrorMessage = "Kỳ lịch đã bắt đầu, không thể đăng ký thêm lịch rảnh." };
        }

        return new PeriodValidationResult { HasError = false, Period = period };
    }

    private static HashSet<int> ResolveValidShiftIds(
        SchedulePeriod period,
        IReadOnlyList<ShiftType> activeShifts)
    {
        var periodShiftIds = period.StaffingRequirements.Select(r => r.ShiftTypeId).ToHashSet();
        return periodShiftIds.Count > 0
            ? periodShiftIds
            : activeShifts.Select(s => s.ShiftTypeId).ToHashSet();
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

        foreach (var key in selectedShiftKeys ?? new List<string>())
        {
            if (!selectedShiftKeySet.Add(key))
            {
                return new SelectedShiftKeyParseResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidShiftSelection,
                    ErrorMessage = $"Ca rảnh bị chọn trùng lặp: {key}."
                };
            }

            var parts = key.Split('_');
            if (parts.Length != 2
                || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var shiftId))
            {
                return new SelectedShiftKeyParseResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidShiftSelection,
                    ErrorMessage = $"Mã ca rảnh không đúng định dạng: {key}."
                };
            }

            if (date < period.WeekStartDate || date > period.WeekEndDate || !validShiftIds.Contains(shiftId))
            {
                return new SelectedShiftKeyParseResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidShiftSelection,
                    ErrorMessage = $"Ca rảnh nằm ngoài kỳ lịch hoặc ca không hoạt động: {key}."
                };
            }

            parsedShifts.Add((date, shiftId));
        }

        return new SelectedShiftKeyParseResult
        {
            HasError = false,
            ParsedShifts = parsedShifts,
            SelectedShiftKeySet = selectedShiftKeySet
        };
    }

    private sealed class PreferenceMapValidationResult
    {
        public bool HasError { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public Dictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel> PreferenceMap { get; init; } = new();
    }

    private static PreferenceMapValidationResult ValidateAndBuildPreferenceMap(
        List<(DateOnly WorkDate, int ShiftTypeId)> parsedShifts,
        HashSet<string> selectedShiftKeySet,
        Dictionary<string, AvailabilityPreferenceLevel> shiftPreferences)
    {
        var map = new Dictionary<(DateOnly, int), AvailabilityPreferenceLevel>();

        foreach (var kvp in shiftPreferences)
        {
            if (!selectedShiftKeySet.Contains(kvp.Key))
            {
                return new PreferenceMapValidationResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidShiftSelection,
                    ErrorMessage = $"Mức nguyện vọng được gửi cho ca rảnh không được chọn: {kvp.Key}."
                };
            }

            var parts = kvp.Key.Split('_');
            if (parts.Length != 2
                || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var shiftId))
            {
                return new PreferenceMapValidationResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidShiftSelection,
                    ErrorMessage = $"Key nguyện vọng không đúng định dạng: {kvp.Key}."
                };
            }

            if (!Enum.IsDefined(kvp.Value))
            {
                return new PreferenceMapValidationResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidEnumValue,
                    ErrorMessage = $"Mức nguyện vọng '{(int)kvp.Value}' không hợp lệ."
                };
            }

            map[(date, shiftId)] = kvp.Value;
        }

        foreach (var (workDate, shiftTypeId) in parsedShifts)
        {
            if (!map.ContainsKey((workDate, shiftTypeId)))
            {
                map[(workDate, shiftTypeId)] = AvailabilityPreferenceLevel.Normal;
            }
        }

        return new PreferenceMapValidationResult
        {
            HasError = false,
            PreferenceMap = map
        };
    }

    private List<AvailabilityDayViewModel> BuildDayViewModels(
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        Dictionary<string, Availability> existingMap)
    {
        var viCulture = new CultureInfo("vi-VN");
        var days = new List<AvailabilityDayViewModel>();

        for (int i = 0; i < 7; i++)
        {
            var date = period.WeekStartDate.AddDays(i);
            var dayName = date.ToString("dddd (dd/MM)", viCulture);

            var dayShifts = fixedShifts.Select(s =>
            {
                var key = $"{date:yyyy-MM-dd}_{s.ShiftTypeId}";
                var existingAvail = existingMap.TryGetValue(key, out var a) ? a : null;
                return new AvailabilityCellViewModel
                {
                    WorkDate = date,
                    ShiftTypeId = s.ShiftTypeId,
                    ShiftCode = s.ShiftCode,
                    ShiftName = s.ShiftName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsSelected = existingAvail is not null,
                    PreferenceLevel = existingAvail?.PreferenceLevel
                        ?? AvailabilityPreferenceLevel.Normal
                };
            }).ToList();

            days.Add(new AvailabilityDayViewModel
            {
                WorkDate = date,
                DayOfWeekName = dayName,
                Shifts = dayShifts
            });
        }

        return days;
    }
}
