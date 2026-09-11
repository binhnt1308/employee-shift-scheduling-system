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
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            var allPeriods = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
            var firstPeriod = allPeriods.Items.OrderByDescending(p => p.SchedulePeriodId).FirstOrDefault();
            if (firstPeriod is null)
            {
                return new ManagerAvailabilityOverviewViewModel
                {
                    PeriodOptions = new List<SchedulePeriodOptionViewModel>(),
                    EmployeeOptions = new List<EmployeeOptionViewModel>(),
                    ShiftOptions = new List<ShiftTypeOptionViewModel>(),
                    Days = new List<AvailabilityDayOverviewViewModel>()
                };
            }

            schedulePeriodId = firstPeriod.SchedulePeriodId;
            period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
            if (period is null)
            {
                return new ManagerAvailabilityOverviewViewModel
                {
                    PeriodOptions = new List<SchedulePeriodOptionViewModel>(),
                    EmployeeOptions = new List<EmployeeOptionViewModel>(),
                    ShiftOptions = new List<ShiftTypeOptionViewModel>(),
                    Days = new List<AvailabilityDayOverviewViewModel>()
                };
            }
        }

        var allPeriodsResult = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
        var periodOptions = allPeriodsResult.Items.Select(p => new SchedulePeriodOptionViewModel
        {
            SchedulePeriodId = p.SchedulePeriodId,
            PeriodName = p.PeriodName
        }).ToList();

        var employees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var employeeOptions = employees.Select(e => new EmployeeOptionViewModel
        {
            EmployeeId = e.EmployeeId,
            DisplayName = $"{e.EmployeeCode} - {e.FullName}"
        }).ToList();

        var fixedShifts = await ResolveFixedShiftsAsync(period, cancellationToken);

        var shiftOptions = fixedShifts.Select(s => new ShiftTypeOptionViewModel
        {
            ShiftTypeId = s.ShiftTypeId,
            ShiftName = s.ShiftName
        }).ToList();

        var avails = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, employeeIdFilter, positionFilter, dateFilter, shiftTypeIdFilter, cancellationToken);

        var days = BuildOverviewDays(period, fixedShifts, avails, dateFilter, shiftTypeIdFilter);

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

        for (int i = 0; i < 7; i++)
        {
            var date = period.WeekStartDate.AddDays(i);
            if (dateFilter.HasValue && dateFilter.Value != date)
            {
                continue;
            }

            var dayName = date.ToString("dddd (dd/MM)", viCulture);
            var shiftCells = new List<ManagerAvailabilityCellViewModel>();

            foreach (var shift in fixedShifts)
            {
                if (shiftTypeIdFilter.HasValue
                    && shiftTypeIdFilter.Value > 0
                    && shift.ShiftTypeId != shiftTypeIdFilter.Value)
                {
                    continue;
                }

                var shiftAvails = avails
                    .Where(a => a.WorkDate == date && a.ShiftTypeId == shift.ShiftTypeId)
                    .ToList();

                var empItems = shiftAvails.Select(a => new ManagerAvailabilityEmployeeItem
                {
                    EmployeeId = a.Employee.EmployeeId,
                    EmployeeCode = a.Employee.EmployeeCode,
                    FullName = a.Employee.FullName,
                    Position = a.Employee.Position,
                    EmploymentType = a.Employee.EmploymentType,
                    Level = a.Employee.Level,
                    // NC-02: Manager phân biệt Normal/Preferred.
                    PreferenceLevel = a.PreferenceLevel
                }).OrderBy(e => e.Position).ThenBy(e => e.FullName).ToList();

                shiftCells.Add(new ManagerAvailabilityCellViewModel
                {
                    WorkDate = date,
                    ShiftTypeId = shift.ShiftTypeId,
                    ShiftCode = shift.ShiftCode,
                    ShiftName = shift.ShiftName,
                    AvailableEmployees = empItems
                });
            }

            days.Add(new AvailabilityDayOverviewViewModel
            {
                WorkDate = date,
                DayOfWeekName = dayName,
                Shifts = shiftCells
            });
        }

        return days;
    }
}
