using System.Text;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

/// <summary>
/// Service tong hop so ca, so ngay va tong so gio lam theo UC11.
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

        if (schedulePeriodId == 0 || !publishedPeriods.Any(p => p.SchedulePeriodId == schedulePeriodId))
        {
            schedulePeriodId = publishedPeriods.First().SchedulePeriodId;
        }

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var publishedSchedule = await _workScheduleRepository.GetPublishedByPeriodIdAsync(
            schedulePeriodId, cancellationToken);

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var employeeOptions = activeEmployees.Select(e => new ReportEmployeeOptionViewModel
        {
            EmployeeId = e.EmployeeId,
            DisplayName = $"{e.EmployeeCode} - {e.FullName}"
        }).ToList();

        var rows = BuildReportRows(activeEmployees, publishedSchedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>(), employeeIdFilter, positionFilter, employmentTypeFilter);

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

        if (schedulePeriodId == 0 || !publishedPeriods.Any(p => p.SchedulePeriodId == schedulePeriodId))
        {
            schedulePeriodId = publishedPeriods.First().SchedulePeriodId;
        }

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
            employee.EmployeeId, null, null);

        var summary = rows.FirstOrDefault();

        return new EmployeeWorkReportOverviewViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            PeriodOptions = publishedPeriods,
            Summary = summary
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
        builder.AppendLine("STT,Mã NV,Họ tên,Vị trí,Hình thức,Số ca,Số ngày,Tổng giờ,Cảnh báo");

        if (overview is not null)
        {
            foreach (var row in overview.Rows)
            {
                var posStr = row.Position == EmployeePosition.Sales ? "Tư vấn" : "Thu ngân";
                var typeStr = row.EmploymentType == EmploymentType.PartTime ? "PT" : "FT";
                builder.AppendLine($"{row.Index},{EscapeCsv(row.EmployeeCode)},{EscapeCsv(row.FullName)},{posStr},{typeStr},{row.TotalShifts},{row.TotalWorkDays},{row.TotalHours},{EscapeCsv(row.Warning)}");
            }
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private async Task<List<ReportPeriodOptionViewModel>> GetPublishedPeriodOptionsAsync(CancellationToken cancellationToken)
    {
        var paged = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
        var list = new List<ReportPeriodOptionViewModel>();

        foreach (var p in paged.Items)
        {
            var sch = await _workScheduleRepository.GetPublishedByPeriodIdAsync(p.SchedulePeriodId, cancellationToken);
            if (sch is not null)
            {
                list.Add(new ReportPeriodOptionViewModel
                {
                    SchedulePeriodId = p.SchedulePeriodId,
                    PeriodName = p.PeriodName
                });
            }
        }

        return list;
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

        var rows = new List<WorkReportRowItemViewModel>();
        int index = 1;

        foreach (var emp in filteredEmps.OrderBy(e => e.EmployeeCode))
        {
            var userAssigns = assignGroup.TryGetValue(emp.EmployeeId, out var list) ? list : new List<ShiftAssignment>();
            int totalShifts = userAssigns.Count;
            int totalWorkDays = userAssigns.Select(a => a.WorkDate).Distinct().Count();
            int totalHours = totalShifts * 4;

            string warning;
            bool isTargetMet;

            if (emp.EmploymentType == EmploymentType.PartTime)
            {
                if (totalShifts == 5) { warning = "Đạt mức dự kiến"; isTargetMet = true; }
                else if (totalShifts < 5) { warning = $"Chưa đủ 5 ca ({totalShifts}/5 ca)"; isTargetMet = false; }
                else { warning = $"Vượt mức 5 ca ({totalShifts}/5 ca)"; isTargetMet = true; }
            }
            else
            {
                if (totalWorkDays == 6) { warning = "Đạt mức dự kiến"; isTargetMet = true; }
                else if (totalWorkDays < 6) { warning = $"Chưa đủ 6 ngày làm việc ({totalWorkDays}/6 ngày)"; isTargetMet = false; }
                else { warning = $"Vượt mức 6 ngày ({totalWorkDays}/6 ngày)"; isTargetMet = true; }
            }

            rows.Add(new WorkReportRowItemViewModel
            {
                Index = index++,
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
            });
        }

        return rows;
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
