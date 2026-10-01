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
            employee.EmployeeId,
            schedulePeriodId,
            cancellationToken);

        var existingMap = BuildExistingAvailabilityMap(existing);
        var registrationState = ResolveRegistrationState(period);
        var days = BuildDayViewModels(period, fixedShifts, existingMap);

        var weeklyPatterns = await _weeklyPatternRepository.GetByEmployeeIdAsync(
            employee.EmployeeId,
            cancellationToken);

        var weeklyPatternViewModel = BuildWeeklyPatternViewModel(weeklyPatterns, fixedShifts);

        return new AvailabilityRegisterViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            AvailabilityDeadline = _clock.ToVietnamTime(period.AvailabilityDeadline),
            IsEditable = registrationState.IsEditable,
            ReadonlyReason = registrationState.ReadonlyReason,
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
            return ServiceResult.Failure(
                ErrorCodes.EmployeeNotFound,
                "Không tìm thấy hồ sơ nhân viên hoặc tài khoản không hoạt động.");
        }

        var periodValidation = await ValidatePeriodForRegistrationAsync(
            schedulePeriodId,
            cancellationToken);
        if (periodValidation.HasError)
        {
            return ServiceResult.Failure(
                periodValidation.ErrorCode!,
                periodValidation.ErrorMessage!);
        }

        var selectionValidation = await ValidateAvailabilitySelectionAsync(
            periodValidation.Period!,
            selectedShiftKeys,
            shiftPreferences,
            cancellationToken);
        if (selectionValidation.HasError)
        {
            return ServiceResult.Failure(
                selectionValidation.ErrorCode!,
                selectionValidation.ErrorMessage!);
        }

        await SaveEmployeeAvailabilityChangesAsync(
            accountId,
            employee,
            periodValidation.Period!,
            selectionValidation.ParsedShifts,
            selectionValidation.PreferenceMap,
            cancellationToken);

        return ServiceResult.Success();
    }

}
