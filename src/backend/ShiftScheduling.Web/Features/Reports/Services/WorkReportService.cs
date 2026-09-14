using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

/// <summary>
/// Service tổng hợp số ca, số ngày và tổng số giờ làm theo UC11 và NC-07.
/// </summary>
public sealed partial class WorkReportService : IWorkReportService
{
    private readonly IWorkScheduleRepository _workScheduleRepository;
    private readonly ISchedulePeriodRepository _periodRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public WorkReportService(
        IWorkScheduleRepository workScheduleRepository,
        ISchedulePeriodRepository periodRepository,
        IEmployeeRepository employeeRepository,
        ILeaveRequestRepository leaveRequestRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _workScheduleRepository = workScheduleRepository;
        _periodRepository = periodRepository;
        _employeeRepository = employeeRepository;
        _leaveRequestRepository = leaveRequestRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var publishedSchedule = await _workScheduleRepository.GetPublishedByPeriodIdAsync(
            schedulePeriodId, cancellationToken);

        var approvedLeaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var leaveLookup = new ApprovedLeaveLookup(approvedLeaves, schedulePeriodId);

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);

        return BuildManagerReportOverview(
            period, publishedPeriods, activeEmployees, publishedSchedule, leaveLookup,
            employeeIdFilter, positionFilter, employmentTypeFilter);
    }

    private ManagerWorkReportOverviewViewModel BuildManagerReportOverview(
        SchedulePeriod period,
        List<ReportPeriodOptionViewModel> publishedPeriods,
        IReadOnlyList<Employee> activeEmployees,
        WorkSchedule? publishedSchedule,
        ApprovedLeaveLookup leaveLookup,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        EmploymentType? employmentTypeFilter)
    {
        var employeeOptions = BuildEmployeeOptions(activeEmployees);
        var assignments = publishedSchedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>();
        bool isPublished = publishedSchedule?.Status == ScheduleStatus.Published;

        var rows = BuildReportRows(
            activeEmployees, assignments, period.SchedulePeriodId, leaveLookup,
            employeeIdFilter, positionFilter, employmentTypeFilter);

        var details = BuildWorkdayDetails(
            assignments, period.SchedulePeriodId, leaveLookup,
            employeeIdFilter, positionFilter, employmentTypeFilter, isPublished);

        var (coverageSummary, fairnessSummary) = CalculateReportSummaries(
            period, publishedSchedule, activeEmployees, assignments, leaveLookup);

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
            Rows = rows,
            Details = details,
            CoverageSummary = coverageSummary,
            FairnessSummary = fairnessSummary
        };
    }

    private static List<ReportEmployeeOptionViewModel> BuildEmployeeOptions(IReadOnlyList<Employee> employees) =>
        employees.Select(e => new ReportEmployeeOptionViewModel
        {
            EmployeeId = e.EmployeeId,
            DisplayName = $"{e.EmployeeCode} - {e.FullName}"
        }).ToList();

    private static (ShiftCoverageSummaryViewModel? Coverage, WorkloadFairnessSummaryViewModel? Fairness) CalculateReportSummaries(
        SchedulePeriod period,
        WorkSchedule? publishedSchedule,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<ShiftAssignment> assignments,
        ApprovedLeaveLookup leaveLookup)
    {
        if (publishedSchedule is null)
        {
            return (null, null);
        }

        var reqs = period.StaffingRequirements.ToList();
        var coverage = CalculateCoverageSummary(reqs, assignments, leaveLookup, period.SchedulePeriodId);
        var fairness = CalculateFairnessSummary(period, activeEmployees, assignments, reqs);
        return (coverage, fairness);
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
        int schedulePeriodId,
        ApprovedLeaveLookup leaveLookup,
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

            rows.Add(BuildSingleReportRow(index++, emp, userAssigns, schedulePeriodId, leaveLookup));
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
        List<ShiftAssignment> userAssigns,
        int schedulePeriodId,
        ApprovedLeaveLookup leaveLookup)
    {
        decimal totalHours = 0m;
        int totalShifts = 0;
        var activeWorkDays = new HashSet<DateOnly>();

        foreach (var a in userAssigns)
        {
            var (_, effectiveHours, _) = CalculateAssignmentHours(a, schedulePeriodId, leaveLookup);
            totalHours += effectiveHours;
            if (effectiveHours > 0m)
            {
                totalShifts++;
                activeWorkDays.Add(a.WorkDate);
            }
        }

        int totalWorkDays = activeWorkDays.Count;
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
}
