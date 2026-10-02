using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Availabilities.Services;

public sealed partial class AvailabilityService
{
    /// <inheritdoc />
    public async Task<ServiceResult> SaveWeeklyPatternAsync(
        int accountId,
        List<WeeklyPatternEntryInputViewModel> entries,
        CancellationToken cancellationToken = default)
    {
        // SE-06: Kiểm quyền trên từng bản ghi – Nhân viên chỉ được lưu mẫu của chính mình.
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null || !employee.IsActive)
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotFound, "Không tìm thấy hồ sơ nhân viên hoặc tài khoản không hoạt động.");
        }

        var validEntries = ValidatePatternEntries(entries ?? new List<WeeklyPatternEntryInputViewModel>());
        if (validEntries.HasError)
        {
            return ServiceResult.Failure(validEntries.ErrorCode!, validEntries.ErrorMessage!);
        }

        var now = _clock.UtcNow;

        await _weeklyPatternRepository.UpsertByEmployeeAsync(
            employee.EmployeeId,
            validEntries.Entries,
            now,
            cancellationToken);

        // SE-16: Ghi nhật ký thao tác lưu mẫu tuần.
        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "SAVE_WEEKLY_PATTERN",
            nameof(WeeklyAvailabilityPattern),
            employee.EmployeeCode,
            null,
            new { Count = validEntries.Entries.Count },
            now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Weekly pattern saved. EmployeeCode: {EmployeeCode}; EntryCount: {Count}",
            employee.EmployeeCode, validEntries.Entries.Count);

        return ServiceResult.Success();
    }

    /// <summary>
    /// Xây dựng WeeklyPatternViewModel từ danh sách pattern và danh sách ca cố định.
    /// </summary>
    internal static WeeklyPatternViewModel BuildWeeklyPatternViewModel(
        List<WeeklyAvailabilityPattern> patterns,
        List<ShiftType> fixedShifts)
    {
        // Ánh xạ ShiftCode → ShiftTypeId từ danh sách ca active hiện tại
        var shiftCodeToId = fixedShifts.ToDictionary(
            s => s.ShiftCode.ToUpperInvariant(),
            s => s.ShiftTypeId);

        var cells = new List<WeeklyPatternCellViewModel>();

        foreach (var pattern in patterns)
        {
            var code = pattern.ShiftCode.ToUpperInvariant();
            if (!shiftCodeToId.TryGetValue(code, out var shiftTypeId))
            {
                // ShiftCode không còn active trong cấu hình hiện tại – bỏ qua khi hiển thị.
                continue;
            }

            cells.Add(new WeeklyPatternCellViewModel
            {
                DayOfWeek = pattern.DayOfWeek,
                ShiftCode = pattern.ShiftCode,
                ShiftTypeId = shiftTypeId,
                PreferenceLevel = pattern.PreferenceLevel
            });
        }

        return new WeeklyPatternViewModel
        {
            Cells = cells,
            ShiftCodeToShiftTypeId = shiftCodeToId
        };
    }

    // ── private helpers ───────────────────────────────────────────────

    private sealed class PatternValidationResult
    {
        public bool HasError { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public List<(int DayOfWeek, string ShiftCode, AvailabilityPreferenceLevel PreferenceLevel)> Entries
        { get; init; } = new();
    }

    private static PatternValidationResult ValidatePatternEntries(
        List<WeeklyPatternEntryInputViewModel> entries)
    {
        var result = new List<(int, string, AvailabilityPreferenceLevel)>();

        foreach (var entry in entries)
        {
            // SE-04: Validate DayOfWeek phạm vi 1..7
            if (entry.DayOfWeek < 1 || entry.DayOfWeek > 7)
            {
                return new PatternValidationResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidEnumValue,
                    ErrorMessage = $"Thứ trong tuần không hợp lệ: {entry.DayOfWeek}. Phải từ 1 (Thứ Hai) đến 7 (Chủ Nhật)."
                };
            }

            // SE-04: Validate ShiftCode chỉ nhận 3 mã cố định (BR03)
            if (!ShiftTypeConstants.IsFixedShiftCode(entry.ShiftCode))
            {
                return new PatternValidationResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.FixedShiftRequired,
                    ErrorMessage = $"Mã ca '{entry.ShiftCode}' không hợp lệ. Chỉ nhận MORNING, AFTERNOON hoặc EVENING."
                };
            }

            // SE-04: Validate PreferenceLevel enum nằm trong phạm vi
            if (!Enum.IsDefined(entry.PreferenceLevel))
            {
                return new PatternValidationResult
                {
                    HasError = true,
                    ErrorCode = ErrorCodes.InvalidEnumValue,
                    ErrorMessage = $"Mức nguyện vọng '{(int)entry.PreferenceLevel}' không hợp lệ."
                };
            }

            result.Add((entry.DayOfWeek, entry.ShiftCode.ToUpperInvariant(), entry.PreferenceLevel));
        }

        return new PatternValidationResult { HasError = false, Entries = result };
    }
}
