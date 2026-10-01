using System.Globalization;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Services;

public sealed partial class AvailabilityService
{
    /// <inheritdoc />
    public async Task<ManagerAvailabilityOverviewViewModel?> GetManagerOverviewAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        var period = await ResolveOverviewPeriodAsync(
            schedulePeriodId,
            cancellationToken);

        if (period is null)
        {
            return CreateEmptyOverview();
        }

        var periodOptions = await BuildPeriodOptionsAsync(cancellationToken);
        var employeeOptions = await BuildEmployeeOptionsAsync(cancellationToken);
        var fixedShifts = await ResolveFixedShiftsAsync(period, cancellationToken);
        var shiftOptions = BuildShiftOptions(fixedShifts);

        var avails = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            period.SchedulePeriodId,
            employeeIdFilter,
            positionFilter,
            dateFilter,
            shiftTypeIdFilter,
            cancellationToken);

        var days = BuildOverviewDays(
            period,
            fixedShifts,
            avails,
            dateFilter,
            shiftTypeIdFilter);

        return CreateOverviewModel(
            period,
            employeeIdFilter,
            positionFilter,
            dateFilter,
            shiftTypeIdFilter,
            periodOptions,
            employeeOptions,
            shiftOptions,
            days);
    }

    private async Task<SchedulePeriod?> ResolveOverviewPeriodAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken)
    {
        var period = await _periodRepository.GetByIdAsync(
            schedulePeriodId,
            true,
            true,
            cancellationToken);

        if (period is not null)
        {
            return period;
        }

        var allPeriods = await _periodRepository.GetPagedAsync(
            1,
            100,
            cancellationToken);

        var latestPeriod = allPeriods.Items
            .OrderByDescending(item => item.SchedulePeriodId)
            .FirstOrDefault();

        if (latestPeriod is null)
        {
            return null;
        }

        return await _periodRepository.GetByIdAsync(
            latestPeriod.SchedulePeriodId,
            true,
            true,
            cancellationToken);
    }

    private static ManagerAvailabilityOverviewViewModel CreateEmptyOverview()
    {
        return new ManagerAvailabilityOverviewViewModel
        {
            PeriodOptions = new List<SchedulePeriodOptionViewModel>(),
            EmployeeOptions = new List<EmployeeOptionViewModel>(),
            ShiftOptions = new List<ShiftTypeOptionViewModel>(),
            Days = new List<AvailabilityDayOverviewViewModel>()
        };
    }

    private async Task<List<SchedulePeriodOptionViewModel>> BuildPeriodOptionsAsync(
        CancellationToken cancellationToken)
    {
        var periods = await _periodRepository.GetPagedAsync(
            1,
            100,
            cancellationToken);

        return periods.Items
            .Select(period => new SchedulePeriodOptionViewModel
            {
                SchedulePeriodId = period.SchedulePeriodId,
                PeriodName = period.PeriodName
            })
            .ToList();
    }

    private async Task<List<EmployeeOptionViewModel>> BuildEmployeeOptionsAsync(
        CancellationToken cancellationToken)
    {
        var employees = await _employeeRepository.GetAllActiveAsync(cancellationToken);

        return employees
            .Select(employee => new EmployeeOptionViewModel
            {
                EmployeeId = employee.EmployeeId,
                DisplayName = $"{employee.EmployeeCode} - {employee.FullName}"
            })
            .ToList();
    }

    private static List<ShiftTypeOptionViewModel> BuildShiftOptions(
        IEnumerable<ShiftType> fixedShifts)
    {
        return fixedShifts
            .Select(shift => new ShiftTypeOptionViewModel
            {
                ShiftTypeId = shift.ShiftTypeId,
                ShiftName = shift.ShiftName
            })
            .ToList();
    }

    private static ManagerAvailabilityOverviewViewModel CreateOverviewModel(
        SchedulePeriod period,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        DateOnly? dateFilter,
        int? shiftTypeIdFilter,
        List<SchedulePeriodOptionViewModel> periodOptions,
        List<EmployeeOptionViewModel> employeeOptions,
        List<ShiftTypeOptionViewModel> shiftOptions,
        List<AvailabilityDayOverviewViewModel> days)
    {
        return new ManagerAvailabilityOverviewViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            EmployeeIdFilter = employeeIdFilter,
            PositionFilter = positionFilter,
            DateFilter = dateFilter,
            ShiftTypeIdFilter = shiftTypeIdFilter,
            PeriodOptions = periodOptions,
            EmployeeOptions = employeeOptions,
            ShiftOptions = shiftOptions,
            Days = days
        };
    }

    // ── private helpers ───────────────────────────────────────────────

    /// <summary>
    /// Lấy 3 ca cố định từ kỳ lịch hoặc fallback sang danh sách active.
    /// Dùng chung cho Registration và Mapping partial.
    /// </summary>
    internal async Task<List<ShiftType>> ResolveFixedShiftsAsync(
        SchedulePeriod period,
        CancellationToken cancellationToken)
    {
        var periodShifts = period.StaffingRequirements
            .Select(r => r.ShiftType)
            .Where(st => st != null)
            .DistinctBy(st => st.ShiftTypeId)
            .OrderBy(st => st.DisplayOrder)
            .ToList();

        if (periodShifts.Count == 3)
        {
            return periodShifts!;
        }

        var allActive = await _shiftTypeRepository.GetActiveShiftsAsync(true, cancellationToken);
        return GetFixedThreeShifts(allActive);
    }

    private static List<ShiftType> GetFixedThreeShifts(IReadOnlyList<ShiftType> allShiftTypes)
    {
        var morning = allShiftTypes.FirstOrDefault(st =>
            st.ShiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase));

        var afternoon = allShiftTypes.FirstOrDefault(st =>
            st.ShiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase));

        var evening = allShiftTypes.FirstOrDefault(st =>
            st.ShiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase));

        if (morning is null || afternoon is null || evening is null)
        {
            return new List<ShiftType>();
        }

        return new List<ShiftType> { morning, afternoon, evening };
    }

    private static List<AvailabilityDayOverviewViewModel> BuildOverviewDays(
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        List<Availability> avails,
        DateOnly? dateFilter,
        int? shiftTypeIdFilter)
    {
        var viCulture = new CultureInfo("vi-VN");
        var days = new List<AvailabilityDayOverviewViewModel>();

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = period.WeekStartDate.AddDays(dayOffset);
            if (dateFilter.HasValue && dateFilter.Value != date)
            {
                continue;
            }

            days.Add(new AvailabilityDayOverviewViewModel
            {
                WorkDate = date,
                DayOfWeekName = date.ToString("dddd (dd/MM)", viCulture),
                Shifts = BuildOverviewShiftCells(
                    date,
                    fixedShifts,
                    avails,
                    shiftTypeIdFilter)
            });
        }

        return days;
    }

    private static List<ManagerAvailabilityCellViewModel> BuildOverviewShiftCells(
        DateOnly date,
        IEnumerable<ShiftType> fixedShifts,
        IEnumerable<Availability> avails,
        int? shiftTypeIdFilter)
    {
        var shiftCells = new List<ManagerAvailabilityCellViewModel>();

        foreach (var shift in fixedShifts)
        {
            if (!ShouldIncludeShift(shift, shiftTypeIdFilter))
            {
                continue;
            }

            shiftCells.Add(new ManagerAvailabilityCellViewModel
            {
                WorkDate = date,
                ShiftTypeId = shift.ShiftTypeId,
                ShiftCode = shift.ShiftCode,
                ShiftName = shift.ShiftName,
                AvailableEmployees = BuildAvailableEmployeeItems(
                    date,
                    shift.ShiftTypeId,
                    avails)
            });
        }

        return shiftCells;
    }

    private static bool ShouldIncludeShift(
        ShiftType shift,
        int? shiftTypeIdFilter)
    {
        return !shiftTypeIdFilter.HasValue
            || shiftTypeIdFilter.Value <= 0
            || shift.ShiftTypeId == shiftTypeIdFilter.Value;
    }

    private static List<ManagerAvailabilityEmployeeItem> BuildAvailableEmployeeItems(
        DateOnly date,
        int shiftTypeId,
        IEnumerable<Availability> avails)
    {
        return avails
            .Where(availability =>
                availability.WorkDate == date
                && availability.ShiftTypeId == shiftTypeId)
            .Select(availability => new ManagerAvailabilityEmployeeItem
            {
                EmployeeId = availability.Employee.EmployeeId,
                EmployeeCode = availability.Employee.EmployeeCode,
                FullName = availability.Employee.FullName,
                Position = availability.Employee.Position,
                EmploymentType = availability.Employee.EmploymentType,
                Level = availability.Employee.Level,
                // NC-02: Manager phân biệt Normal/Preferred.
                PreferenceLevel = availability.PreferenceLevel
            })
            .OrderBy(employee => employee.Position)
            .ThenBy(employee => employee.FullName)
            .ToList();
    }
}
