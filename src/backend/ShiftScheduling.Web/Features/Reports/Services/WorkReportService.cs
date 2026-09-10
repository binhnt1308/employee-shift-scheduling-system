using System.Text;
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
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Reports.Services;

/// <summary>
/// Service tổng hợp số ca, số ngày và tổng số giờ làm theo UC11 và NC-07.
/// </summary>
public sealed class WorkReportService : IWorkReportService
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
        var employeeOptions = activeEmployees.Select(e => new ReportEmployeeOptionViewModel
        {
            EmployeeId = e.EmployeeId,
            DisplayName = $"{e.EmployeeCode} - {e.FullName}"
        }).ToList();

        var assignments = publishedSchedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>();

        var rows = BuildReportRows(
            activeEmployees,
            assignments,
            period.SchedulePeriodId,
            leaveLookup,
            employeeIdFilter,
            positionFilter,
            employmentTypeFilter);

        var details = BuildWorkdayDetails(
            assignments,
            period.SchedulePeriodId,
            leaveLookup,
            employeeIdFilter,
            positionFilter,
            employmentTypeFilter,
            publishedSchedule?.Status == ScheduleStatus.Published);

        var coverageSummary = publishedSchedule is not null
            ? CalculateCoverageSummary(
                period.StaffingRequirements.ToList(),
                assignments)
            : null;

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
            CoverageSummary = coverageSummary
        };
    }

    private List<WorkdayDetailItemViewModel> BuildWorkdayDetails(
        List<ShiftAssignment> assignments,
        int schedulePeriodId,
        ApprovedLeaveLookup leaveLookup,
        int? employeeIdFilter,
        EmployeePosition? positionFilter,
        EmploymentType? employmentTypeFilter,
        bool isPublished)
    {
        var vietnamNow = _clock.ToVietnamTime(_clock.UtcNow);

        var filteredAssignments = assignments.AsEnumerable();

        if (employeeIdFilter.HasValue && employeeIdFilter.Value > 0)
        {
            filteredAssignments = filteredAssignments.Where(a => a.EmployeeId == employeeIdFilter.Value);
        }

        if (positionFilter.HasValue)
        {
            filteredAssignments = filteredAssignments.Where(a => a.Employee?.Position == positionFilter.Value);
        }

        if (employmentTypeFilter.HasValue)
        {
            filteredAssignments = filteredAssignments.Where(a => a.Employee?.EmploymentType == employmentTypeFilter.Value);
        }

        var details = new List<WorkdayDetailItemViewModel>();

        foreach (var a in filteredAssignments
            .OrderBy(a => a.WorkDate)
            .ThenBy(a => a.ShiftType?.StartTime ?? TimeOnly.MinValue)
            .ThenBy(a => a.Employee?.EmployeeCode ?? string.Empty))
        {
            var (systemHours, effectiveHours, hasApprovedLeave) = CalculateAssignmentHours(a, schedulePeriodId, leaveLookup);
            decimal plannedHours = a.ShiftType?.DurationHours ?? 0m;

            bool hasEnded = false;
            if (a.ShiftType != null)
            {
                var shiftEndDate = a.WorkDate;
                if (a.ShiftType.EndTime <= a.ShiftType.StartTime)
                {
                    shiftEndDate = shiftEndDate.AddDays(1);
                }
                var shiftEndDateTime = shiftEndDate.ToDateTime(a.ShiftType.EndTime);
                hasEnded = vietnamNow >= shiftEndDateTime;
            }

            bool canAdjust = isPublished && hasEnded;

            details.Add(new WorkdayDetailItemViewModel
            {
                ShiftAssignmentId = a.ShiftAssignmentId,
                EmployeeId = a.EmployeeId,
                EmployeeCode = a.Employee?.EmployeeCode ?? string.Empty,
                FullName = a.Employee?.FullName ?? string.Empty,
                WorkDate = a.WorkDate,
                ShiftTypeId = a.ShiftTypeId,
                ShiftName = a.ShiftType?.ShiftName ?? string.Empty,
                ShiftTimeRange = a.ShiftType != null ? $"{a.ShiftType.StartTime:HH\\:mm} - {a.ShiftType.EndTime:HH\\:mm}" : string.Empty,
                PlannedHours = plannedHours,
                ActualWorkedHours = a.ActualWorkedHours,
                SystemCalculatedHours = systemHours,
                EffectiveWorkedHours = effectiveHours,
                WorkdayAdjustmentReason = a.WorkdayAdjustmentReason,
                HasApprovedLeave = hasApprovedLeave,
                CanAdjust = canAdjust
            });
        }

        return details;
    }

    private static ShiftCoverageSummaryViewModel CalculateCoverageSummary(
        IReadOnlyList<StaffingRequirement> requirements,
        IReadOnlyList<ShiftAssignment> assignments)
    {
        if (requirements.Count == 0)
        {
            return new ShiftCoverageSummaryViewModel
            {
                CoverageRate = null
            };
        }

        int totalRequiredSales = 0;
        int totalCoveredSales = 0;
        int totalRequiredCashier = 0;
        int totalCoveredCashier = 0;

        var assignmentGroups = assignments
            .GroupBy(a => (a.WorkDate, a.ShiftTypeId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var req in requirements)
        {
            totalRequiredSales += req.RequiredSalesStaff;
            totalRequiredCashier += req.RequiredCashiers;

            if (assignmentGroups.TryGetValue((req.WorkDate, req.ShiftTypeId), out var shiftAssigns))
            {
                int assignedSales = shiftAssigns.Count(a => a.Employee?.Position == EmployeePosition.Sales);
                int assignedCashier = shiftAssigns.Count(a => a.Employee?.Position == EmployeePosition.Cashier);

                // NC-08: Giới hạn slot đã phủ theo từng ngày-ca-vị trí để ca dư không bù ca thiếu.
                totalCoveredSales += Math.Min(assignedSales, req.RequiredSalesStaff);
                totalCoveredCashier += Math.Min(assignedCashier, req.RequiredCashiers);
            }
        }

        int totalRequired = totalRequiredSales + totalRequiredCashier;
        int totalCovered = totalCoveredSales + totalCoveredCashier;

        return new ShiftCoverageSummaryViewModel
        {
            TotalRequiredSlots = totalRequired,
            TotalCoveredSlots = totalCovered,
            RequiredSalesSlots = totalRequiredSales,
            CoveredSalesSlots = totalCoveredSales,
            RequiredCashierSlots = totalRequiredCashier,
            CoveredCashierSlots = totalCoveredCashier,
            CoverageRate = totalRequired > 0 ? totalCovered * 100m / totalRequired : null
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

        var approvedLeaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var leaveLookup = new ApprovedLeaveLookup(approvedLeaves, schedulePeriodId);

        var rows = BuildReportRows(
            new List<Employee> { employee },
            publishedSchedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>(),
            schedulePeriodId,
            leaveLookup,
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

    /// <inheritdoc />
    public async Task<ServiceResult> AdjustWorkdayHoursAsync(
        int actorAccountId,
        AdjustWorkdayHoursRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ShiftAssignmentId <= 0)
        {
            return ServiceResult.Failure("InvalidShiftAssignment", "Mã phân công không hợp lệ.");
        }

        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            request.SchedulePeriodId,
            asNoTracking: false,
            includeAssignments: true,
            cancellationToken);

        if (schedule == null)
        {
            return ServiceResult.Failure("ScheduleNotFound", "Không tìm thấy lịch làm việc của kỳ.");
        }

        if (schedule.Status != ScheduleStatus.Published)
        {
            return ServiceResult.Failure("ScheduleNotPublished", "Chỉ có thể điều chỉnh ngày công trên lịch làm việc đã công bố.");
        }

        var assignment = schedule.ShiftAssignments
            .FirstOrDefault(a => a.ShiftAssignmentId == request.ShiftAssignmentId);

        if (assignment == null)
        {
            return ServiceResult.Failure("ShiftAssignmentNotFound", "Không tìm thấy phân công ca làm việc trong kỳ lịch.");
        }

        if (assignment.ShiftType == null)
        {
            return ServiceResult.Failure("ShiftTypeNotFound", "Không tìm thấy thông tin loại ca.");
        }

        var vietnamNow = _clock.ToVietnamTime(_clock.UtcNow);
        var shiftEndDate = assignment.WorkDate;
        if (assignment.ShiftType.EndTime <= assignment.ShiftType.StartTime)
        {
            shiftEndDate = shiftEndDate.AddDays(1);
        }
        var shiftEndDateTime = shiftEndDate.ToDateTime(assignment.ShiftType.EndTime);
        if (vietnamNow < shiftEndDateTime)
        {
            return ServiceResult.Failure("ShiftNotEnded", "Chỉ được điều chỉnh giờ công sau khi ca làm việc đã kết thúc.");
        }

        var approvedLeaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            request.SchedulePeriodId, cancellationToken);
        var leaveLookup = new ApprovedLeaveLookup(approvedLeaves, request.SchedulePeriodId);

        bool hasApprovedLeave = leaveLookup.HasApprovedLeave(
            assignment.EmployeeId, request.SchedulePeriodId, assignment.WorkDate, assignment.ShiftTypeId);

        decimal systemCalculatedHours = hasApprovedLeave ? 0m : assignment.ShiftType.DurationHours;
        decimal currentEffectiveHours = assignment.ActualWorkedHours ?? systemCalculatedHours;

        var prevValues = new
        {
            assignment.ActualWorkedHours,
            assignment.WorkdayAdjustmentReason,
            EffectiveWorkedHours = currentEffectiveHours
        };

        if (request.IsReset)
        {
            if (!assignment.ActualWorkedHours.HasValue && string.IsNullOrEmpty(assignment.WorkdayAdjustmentReason))
            {
                // No-op
                return ServiceResult.Success();
            }

            assignment.ActualWorkedHours = null;
            assignment.WorkdayAdjustmentReason = null;
            assignment.UpdatedAt = _clock.UtcNow;

            var newValues = new
            {
                ActualWorkedHours = (decimal?)null,
                WorkdayAdjustmentReason = (string?)null,
                EffectiveWorkedHours = systemCalculatedHours
            };

            _auditLogRepository.Add(AuditLogFactory.Create(
                actorAccountId,
                "ADJUST_WORKDAY_HOURS",
                nameof(ShiftAssignment),
                assignment.ShiftAssignmentId.ToString(),
                prevValues,
                newValues,
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }

        if (!request.ActualWorkedHours.HasValue)
        {
            return ServiceResult.Failure("HoursRequired", "Vui lòng nhập số giờ làm việc thực tế.");
        }

        decimal actualHours = request.ActualWorkedHours.Value;
        if (actualHours < 0m || actualHours > 24m)
        {
            return ServiceResult.Failure("HoursOutOfRange",
                "Số giờ làm việc thực tế phải từ 0 đến 24 giờ.");
        }

        if (actualHours == systemCalculatedHours)
        {
            if (!assignment.ActualWorkedHours.HasValue && string.IsNullOrEmpty(assignment.WorkdayAdjustmentReason))
            {
                // No-op
                return ServiceResult.Success();
            }

            assignment.ActualWorkedHours = null;
            assignment.WorkdayAdjustmentReason = null;
            assignment.UpdatedAt = _clock.UtcNow;

            var newValues = new
            {
                ActualWorkedHours = (decimal?)null,
                WorkdayAdjustmentReason = (string?)null,
                EffectiveWorkedHours = systemCalculatedHours
            };

            _auditLogRepository.Add(AuditLogFactory.Create(
                actorAccountId,
                "ADJUST_WORKDAY_HOURS",
                nameof(ShiftAssignment),
                assignment.ShiftAssignmentId.ToString(),
                prevValues,
                newValues,
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }

        string trimmedReason = request.WorkdayAdjustmentReason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            return ServiceResult.Failure("ReasonRequired", "Bắt buộc phải nhập lý do khi điều chỉnh giờ công khác với giờ hệ thống tính.");
        }

        if (trimmedReason.Length > 500)
        {
            return ServiceResult.Failure("ReasonTooLong", "Lý do điều chỉnh không được vượt quá 500 ký tự.");
        }

        if (assignment.ActualWorkedHours == actualHours && assignment.WorkdayAdjustmentReason == trimmedReason)
        {
            // No-op
            return ServiceResult.Success();
        }

        assignment.ActualWorkedHours = actualHours;
        assignment.WorkdayAdjustmentReason = trimmedReason;
        assignment.UpdatedAt = _clock.UtcNow;

        var updatedNewValues = new
        {
            ActualWorkedHours = assignment.ActualWorkedHours,
            WorkdayAdjustmentReason = assignment.WorkdayAdjustmentReason,
            EffectiveWorkedHours = actualHours
        };

        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "ADJUST_WORKDAY_HOURS",
            nameof(ShiftAssignment),
            assignment.ShiftAssignmentId.ToString(),
            prevValues,
            updatedNewValues,
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    private static (decimal SystemCalculatedHours, decimal EffectiveWorkedHours, bool HasApprovedLeave) CalculateAssignmentHours(
        ShiftAssignment assignment,
        int schedulePeriodId,
        ApprovedLeaveLookup leaveLookup)
    {
        bool hasApprovedLeave = leaveLookup.HasApprovedLeave(
            assignment.EmployeeId, schedulePeriodId, assignment.WorkDate, assignment.ShiftTypeId);
        decimal plannedHours = assignment.ShiftType?.DurationHours ?? 0m;
        decimal systemCalculatedHours = hasApprovedLeave ? 0m : plannedHours;
        decimal effectiveWorkedHours = assignment.ActualWorkedHours ?? systemCalculatedHours;
        return (systemCalculatedHours, effectiveWorkedHours, hasApprovedLeave);
    }

    private sealed class ApprovedLeaveLookup
    {
        private readonly HashSet<(int EmployeeId, int SchedulePeriodId, DateOnly LeaveDate, int ShiftTypeId)> _shiftLeaves = new();
        private readonly HashSet<(int EmployeeId, int SchedulePeriodId, DateOnly LeaveDate)> _fullDayLeaves = new();

        public ApprovedLeaveLookup(IEnumerable<LeaveRequest> approvedLeaves, int schedulePeriodId)
        {
            foreach (var leave in approvedLeaves)
            {
                if (leave.Status == LeaveRequestStatus.Approved && leave.SchedulePeriodId == schedulePeriodId)
                {
                    if (leave.ShiftTypeId.HasValue)
                    {
                        _shiftLeaves.Add((leave.EmployeeId, leave.SchedulePeriodId, leave.LeaveDate, leave.ShiftTypeId.Value));
                    }
                    else
                    {
                        _fullDayLeaves.Add((leave.EmployeeId, leave.SchedulePeriodId, leave.LeaveDate));
                    }
                }
            }
        }

        public bool HasApprovedLeave(int employeeId, int schedulePeriodId, DateOnly workDate, int shiftTypeId)
        {
            return _fullDayLeaves.Contains((employeeId, schedulePeriodId, workDate))
                || _shiftLeaves.Contains((employeeId, schedulePeriodId, workDate, shiftTypeId));
        }
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

    private static string EscapeCsv(string text)
    {
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }
}
