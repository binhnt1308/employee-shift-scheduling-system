using System.Text;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

/// <summary>
/// Service tổng hợp số ca, số ngày và tổng số giờ làm theo UC11.
/// </summary>
public sealed class WorkReportService : IWorkReportService
{
    private readonly IWorkScheduleRepository _workScheduleRepository;
    private readonly ISchedulePeriodRepository _periodRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public WorkReportService(
        IWorkScheduleRepository workScheduleRepository,
        ISchedulePeriodRepository periodRepository,
        IEmployeeRepository employeeRepository)
    {
        _workScheduleRepository = workScheduleRepository;
        _periodRepository = periodRepository;
        _employeeRepository = employeeRepository;
    }

    /// <inheritdoc />
    public async Task<ManagerWorkReportOverviewViewModel?> GetManagerReportOverviewAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        EmploymentType? employmentTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var publishedPeriods = await GetPublishedPeriodOptionsAsync(cancellationToken);
        if (!publishedPeriods.Any())
        {
            return new ManagerWorkReportOverviewViewModel
            {
                PeriodOptions = new List<ReportPeriodOptionViewModel>(),
                Rows = new List<WorkReportRowItemViewModel>()
            };
        }

        schedulePeriodId = ResolveSchedulePeriodId(schedulePeriodId, publishedPeriods);

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var publishedSchedule = await _workScheduleRepository.GetPublishedByPeriodIdAsync(
            schedulePeriodId, cancellationToken);

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);

        return BuildManagerReportOverview(
            period, publishedPeriods, activeEmployees, publishedSchedule,
            employeeIdFilter, positionFilter, employmentTypeFilter);
    }

    private static ManagerWorkReportOverviewViewModel BuildManagerReportOverview(
        SchedulePeriod period,
        List<ReportPeriodOptionViewModel> publishedPeriods,
        IReadOnlyList<Employee> activeEmployees,
        WorkSchedule? publishedSchedule,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        EmploymentType? employmentTypeFilter)
    {
        var employeeOptions = activeEmployees.Select(e => new ReportEmployeeOptionViewModel
        {
            EmployeeId = e.EmployeeId,
            DisplayName = $"{e.EmployeeCode} - {e.FullName}"
        }).ToList();

        var rows = BuildReportRows(
            activeEmployees,
            publishedSchedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>(),
            employeeIdFilter,
            positionFilter,
            employmentTypeFilter);

        return new ManagerWorkReportOverviewViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            EmployeeIdFilter = employeeIdFilter,
            PositionFilter = positionFilter,
            EmploymentTypeFilter = employmentTypeFilter,
            PeriodOptions = publishedPeriods,
            EmployeeOptions = employeeOptions,
            Rows = rows
        };
    }

    /// <inheritdoc />
    public async Task<EmployeeWorkReportOverviewViewModel?> GetEmployeeOwnReportAsync(
        int accountId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var publishedPeriods = await GetPublishedPeriodOptionsAsync(cancellationToken);
        if (!publishedPeriods.Any())
        {
            return new EmployeeWorkReportOverviewViewModel
            {
                PeriodOptions = new List<ReportPeriodOptionViewModel>()
            };
        }

        schedulePeriodId = ResolveSchedulePeriodId(schedulePeriodId, publishedPeriods);

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var publishedSchedule = await _workScheduleRepository.GetPublishedByPeriodIdAsync(
            schedulePeriodId, cancellationToken);

        var rows = BuildReportRows(
            new List<Employee> { employee },
            publishedSchedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>(),
            employee.EmployeeId,
            null,
            null);

        return new EmployeeWorkReportOverviewViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            PeriodOptions = publishedPeriods,
            Summary = rows.FirstOrDefault()
        };
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateReportCsvAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        EmploymentType? employmentTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var overview = await GetManagerReportOverviewAsync(
            schedulePeriodId, employeeIdFilter, positionFilter, employmentTypeFilter, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("STT,Mã NV,Họ tên,Vị trí,Hình thức,Số ca,Số ngày,Tổng giờ,Cảnh báo / Mức dự kiến");

        if (overview is not null)
        {
            foreach (var row in overview.Rows)
            {
                var posStr = row.Position == EmployeePosition.Sales ? "Tư vấn" : "Thu ngân";
                var typeStr = row.EmploymentType == EmploymentType.PartTime ? "PT" : "FT";
                var code = EscapeCsv(row.EmployeeCode);
                var name = EscapeCsv(row.FullName);
                var hoursStr = row.TotalHours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var warn = EscapeCsv(row.Warning);
                builder.AppendLine(
                    $"{row.Index},{code},{name},{posStr},{typeStr}," +
                    $"{row.TotalShifts},{row.TotalWorkDays},{hoursStr},{warn}");
            }
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static int ResolveSchedulePeriodId(
        int schedulePeriodId,
        List<ReportPeriodOptionViewModel> publishedPeriods)
    {
        if (schedulePeriodId == 0 || !publishedPeriods.Any(p => p.SchedulePeriodId == schedulePeriodId))
        {
            return publishedPeriods.First().SchedulePeriodId;
        }

        return schedulePeriodId;
    }

    private async Task<List<ReportPeriodOptionViewModel>> GetPublishedPeriodOptionsAsync(
        CancellationToken cancellationToken)
    {
        var paged = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
        if (!paged.Items.Any())
        {
            return new List<ReportPeriodOptionViewModel>();
        }

        var periodIds = paged.Items.Select(p => p.SchedulePeriodId).ToList();
        var publishedSet = await _workScheduleRepository.GetPublishedPeriodIdsAsync(
            periodIds, cancellationToken);

        return paged.Items
            .Where(p => publishedSet.Contains(p.SchedulePeriodId))
            .Select(p => new ReportPeriodOptionViewModel
            {
                SchedulePeriodId = p.SchedulePeriodId,
                PeriodName = p.PeriodName
            })
            .ToList();
    }

    private static List<WorkReportRowItemViewModel> BuildReportRows(
        IReadOnlyList<Employee> employees,
        List<ShiftAssignment> publishedAssignments,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        EmploymentType? employmentTypeFilter)
    {
        var assignGroup = publishedAssignments
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var filteredEmps = FilterEmployees(employees, employeeIdFilter, positionFilter, employmentTypeFilter);

        var rows = new List<WorkReportRowItemViewModel>();
        int index = 1;

        foreach (var emp in filteredEmps.OrderBy(e => e.EmployeeCode))
        {
            var userAssigns = assignGroup.TryGetValue(emp.EmployeeId, out var list)
                ? list
                : new List<ShiftAssignment>();

            rows.Add(BuildSingleReportRow(index++, emp, userAssigns));
        }

        return rows;
    }

    private static IEnumerable<Employee> FilterEmployees(
        IReadOnlyList<Employee> employees,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        EmploymentType? employmentTypeFilter)
    {
        var filteredEmps = employees.AsEnumerable();

        if (employeeIdFilter.HasValue && employeeIdFilter.Value > 0)
        {
            filteredEmps = filteredEmps.Where(e => e.EmployeeId == employeeIdFilter.Value);
        }

        if (positionFilter.HasValue)
        {
            filteredEmps = filteredEmps.Where(e => e.Position == positionFilter.Value);
        }

        if (employmentTypeFilter.HasValue)
        {
            filteredEmps = filteredEmps.Where(e => e.EmploymentType == employmentTypeFilter.Value);
        }

        return filteredEmps;
    }

    private static WorkReportRowItemViewModel BuildSingleReportRow(
        int index,
        Employee emp,
        List<ShiftAssignment> userAssigns)
    {
        int totalShifts = userAssigns.Count;
        int totalWorkDays = userAssigns.Select(a => a.WorkDate).Distinct().Count();
        decimal totalHours = userAssigns.Sum(a => a.ShiftType?.DurationHours ?? 0m);

        var (warning, isTargetMet) = EvaluateWorkloadTarget(emp, totalShifts, totalWorkDays);

        return new WorkReportRowItemViewModel
        {
            Index = index,
            EmployeeId = emp.EmployeeId,
            EmployeeCode = emp.EmployeeCode,
            FullName = emp.FullName,
            Position = emp.Position,
            EmploymentType = emp.EmploymentType,
            TotalShifts = totalShifts,
            TotalWorkDays = totalWorkDays,
            TotalHours = totalHours,
            Warning = warning,
            IsTargetMet = isTargetMet
        };
    }

    private static (string Warning, bool IsTargetMet) EvaluateWorkloadTarget(
        Employee emp,
        int totalShifts,
        int totalWorkDays)
    {
        if (emp.EmploymentType == EmploymentType.PartTime)
        {
            if (totalShifts == 5) return ("Đạt mức dự kiến", true);
            if (totalShifts < 5) return ($"Chưa đủ 5 ca ({totalShifts}/5 ca)", false);
            return ($"Vượt mức 5 ca ({totalShifts}/5 ca)", true);
        }
        else
        {
            if (totalWorkDays == 6) return ("Đạt mức dự kiến", true);
            if (totalWorkDays < 6) return ($"Chưa đủ 6 ngày làm việc ({totalWorkDays}/6 ngày)", false);
            return ($"Vượt mức 6 ngày ({totalWorkDays}/6 ngày)", true);
        }
    }

    private static string EscapeCsv(string text)
    {
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }
}
