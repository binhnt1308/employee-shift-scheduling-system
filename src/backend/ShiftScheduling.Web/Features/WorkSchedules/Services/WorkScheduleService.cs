using System.Globalization;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Email;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Service ho tro phan cong (UC05), sinh lich OR-Tools (UC06) va cong bo lich (UC07).
/// </summary>
public partial class WorkScheduleService : IWorkScheduleService
{
    private readonly IWorkScheduleRepository _workScheduleRepository;
    private readonly ISchedulePeriodRepository _periodRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAvailabilityRepository _availabilityRepository;
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly ISchedulingValidationService _validationService;
    private readonly IOrToolsSchedulingService _orToolsService;
    private readonly INotificationRepository _notificationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<WorkScheduleService> _logger;
    private readonly IEmailSender? _emailSender;

    public WorkScheduleService(
        IWorkScheduleRepository workScheduleRepository,
        ISchedulePeriodRepository periodRepository,
        IShiftTypeRepository shiftTypeRepository,
        IEmployeeRepository employeeRepository,
        IAvailabilityRepository availabilityRepository,
        ILeaveRequestRepository leaveRequestRepository,
        ISchedulingValidationService validationService,
        IOrToolsSchedulingService orToolsService,
        INotificationRepository notificationRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<WorkScheduleService> logger,
        IEmailSender? emailSender = null)
    {
        _workScheduleRepository = workScheduleRepository;
        _periodRepository = periodRepository;
        _shiftTypeRepository = shiftTypeRepository;
        _employeeRepository = employeeRepository;
        _availabilityRepository = availabilityRepository;
        _leaveRequestRepository = leaveRequestRepository;
        _validationService = validationService;
        _orToolsService = orToolsService;
        _notificationRepository = notificationRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
        _emailSender = emailSender;
    }

    /// <inheritdoc />
    public async Task<WorkScheduleIndexViewModel> GetIndexAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var pagedPeriods = await _periodRepository.GetPagedAsync(page, pageSize, cancellationToken);
        var periodItems = new List<WorkSchedulePeriodItemViewModel>();

        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));

        foreach (var period in pagedPeriods.Items)
        {
            var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
                period.SchedulePeriodId, true, true, cancellationToken);

            var isRegOpen = SchedulePeriodStatusHelper.IsRegistrationEffectivelyOpen(period, now, currentDate);

            var canSchedule = CanSchedulePeriod(period, schedule, now, currentDate);

            string statusName;
            string badgeClass;
            if (period.RegistrationStatus == RegistrationStatus.Preparing)
            {
                statusName = "Đang chuẩn bị";
                badgeClass = "badge bg-secondary";
            }
            else if (isRegOpen)
            {
                statusName = "Đang mở đăng ký";
                badgeClass = "badge bg-success";
            }
            else
            {
                statusName = "Đã đóng đăng ký";
                badgeClass = "badge bg-dark";
            }

            periodItems.Add(new WorkSchedulePeriodItemViewModel
            {
                SchedulePeriodId = period.SchedulePeriodId,
                PeriodName = period.PeriodName,
                WeekStartDate = period.WeekStartDate,
                WeekEndDate = period.WeekEndDate,
                RegistrationStatus = period.RegistrationStatus,
                IsRegistrationEffectivelyOpen = isRegOpen,
                EffectiveRegistrationStatusName = statusName,
                EffectiveRegistrationBadgeClass = badgeClass,
                WorkScheduleId = schedule?.WorkScheduleId,
                ScheduleStatus = schedule?.Status,
                PublishedAt = schedule?.PublishedAt,
                TotalAssignmentsCount = schedule?.ShiftAssignments.Count ?? 0,
                CanSchedule = canSchedule
            });
        }

        return new WorkScheduleIndexViewModel
        {
            Periods = periodItems
        };
    }

    /// <inheritdoc />
    public async Task<WorkScheduleEditViewModel?> GetEditGridAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null) return null;

        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, true, true, cancellationToken);
        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));

        if (!CanSchedulePeriod(period, schedule, now, currentDate)) return null;

        if (schedule is null)
        {
            schedule = await _workScheduleRepository.EnsureDraftExistsAsync(
                schedulePeriodId, accountId, now, cancellationToken);
            schedule = await _workScheduleRepository.GetByPeriodIdAsync(
                schedulePeriodId, true, true, cancellationToken);
        }
        if (schedule is null) return null;

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, null, null, cancellationToken);
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = ResolveFixedShifts(period, allShifts);

        var validationResult = _validationService.ValidateSchedule(
            period, activeEmployees, availabilities, leaves, period.StaffingRequirements.ToList(),
            schedule.ShiftAssignments.ToList(), allShifts);
        var days = BuildGridDays(
            period, fixedShifts, period.StaffingRequirements.ToList(),
            schedule.ShiftAssignments.ToList(), activeEmployees, availabilities, leaves);

        return CreateEditViewModel(period, schedule, validationResult, days, now, currentDate);
    }

    private static List<ShiftType> ResolveFixedShifts(SchedulePeriod period, IReadOnlyList<ShiftType> allShifts)
    {
        var periodShifts = period.StaffingRequirements
            .Select(r => r.ShiftType)
            .Where(st => st != null)
            .DistinctBy(st => st.ShiftTypeId)
            .OrderBy(st => st.DisplayOrder)
            .ToList();
        return periodShifts.Count == 3 ? periodShifts : GetFixedThreeShifts(allShifts);
    }

    private static WorkScheduleEditViewModel CreateEditViewModel(
        SchedulePeriod period,
        WorkSchedule schedule,
        ScheduleValidationResultViewModel validationResult,
        List<SchedulingDayGridViewModel> days,
        DateTime now,
        DateOnly currentDate)
    {
        var statusResult = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, schedule, now, currentDate);
        return new WorkScheduleEditViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            RegistrationStatus = period.RegistrationStatus,
            WorkScheduleId = schedule.WorkScheduleId,
            Status = schedule.Status,
            EffectiveStatusName = statusResult.StatusName,
            DisplayBadgeClass = statusResult.DisplayBadgeClass,
            IsGeneratedByORTools = schedule.IsGeneratedByORTools,
            CanEditAssignments = schedule.Status == ScheduleStatus.Draft,
            CanClose = SchedulePeriodStatusHelper.CanCloseSchedule(period, schedule, currentDate),
            RowVersion = schedule.RowVersion != null ? Convert.ToBase64String(schedule.RowVersion) : string.Empty,
            Days = days,
            ValidationResult = validationResult
        };
    }

    private static List<SchedulingDayGridViewModel> BuildGridDays(
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        List<StaffingRequirement> requirements,
        List<ShiftAssignment> assignments,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability>? availabilities = null,
        IReadOnlyList<LeaveRequest>? approvedLeaves = null)
    {
        var empMap = activeEmployees.ToDictionary(e => e.EmployeeId);
        var preferredSet = availabilities?
            .Where(a => a.PreferenceLevel == AvailabilityPreferenceLevel.Preferred)
            .Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId))
            .ToHashSet();
        var viCulture = new CultureInfo("vi-VN");
        var days = new List<SchedulingDayGridViewModel>(7);

        for (int i = 0; i < 7; i++)
        {
            var date = period.WeekStartDate.AddDays(i);
            var shiftCells = fixedShifts
                .Select(s => BuildShiftCell(date, s, requirements, assignments, empMap, preferredSet, approvedLeaves))
                .ToList();

            days.Add(new SchedulingDayGridViewModel
            {
                WorkDate = date,
                DayOfWeekName = date.ToString("dddd (dd/MM)", viCulture),
                Shifts = shiftCells
            });
        }

        return days;
    }

    private static SchedulingShiftCellViewModel BuildShiftCell(
        DateOnly date, ShiftType shift, List<StaffingRequirement> requirements,
        List<ShiftAssignment> assignments, Dictionary<int, Employee> empMap,
        HashSet<(int, DateOnly, int)>? preferredSet, IReadOnlyList<LeaveRequest>? approvedLeaves)
    {
        var req = requirements.FirstOrDefault(r => r.WorkDate == date && r.ShiftTypeId == shift.ShiftTypeId);
        int reqSales = req?.RequiredSalesStaff ?? 0, reqCashier = req?.RequiredCashiers ?? 0;
        var shiftAssigns = assignments.Where(a => a.WorkDate == date && a.ShiftTypeId == shift.ShiftTypeId).ToList();
        var assignedItems = shiftAssigns.Select(a =>
        {
            empMap.TryGetValue(a.EmployeeId, out var emp);
            var leave = approvedLeaves?.FirstOrDefault(l => l.EmployeeId == a.EmployeeId
                && l.LeaveDate == a.WorkDate && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == a.ShiftTypeId));
            return new AssignedEmployeeItemViewModel
            {
                ShiftAssignmentId = a.ShiftAssignmentId, EmployeeId = a.EmployeeId,
                EmployeeCode = emp?.EmployeeCode ?? a.Employee?.EmployeeCode ?? string.Empty,
                FullName = emp?.FullName ?? a.Employee?.FullName ?? string.Empty,
                Position = emp?.Position ?? a.Employee?.Position ?? EmployeePosition.Sales,
                EmploymentType = emp?.EmploymentType ?? a.Employee?.EmploymentType ?? EmploymentType.PartTime,
                Level = emp?.Level ?? a.Employee?.Level ?? EmployeeLevel.Level1, Source = a.Source,
                IsPreferred = preferredSet?.Contains((a.EmployeeId, a.WorkDate, a.ShiftTypeId)) ?? false,
                HasApprovedLeave = leave != null, ApprovedLeaveRequestId = leave?.LeaveRequestId
            };
        }).OrderBy(e => e.Position).ThenBy(e => e.FullName).ToList();

        int assignedSales = assignedItems.Count(e => e.Position == EmployeePosition.Sales);
        int assignedCashier = assignedItems.Count(e => e.Position == EmployeePosition.Cashier);
        bool hasLevel2Plus = assignedItems.Any(e => e.Level >= EmployeeLevel.Level2);

        return new SchedulingShiftCellViewModel
        {
            WorkDate = date, ShiftTypeId = shift.ShiftTypeId, ShiftCode = shift.ShiftCode,
            ShiftName = shift.ShiftName, StartTime = shift.StartTime, EndTime = shift.EndTime,
            RequiredSales = reqSales, RequiredCashier = reqCashier,
            AssignedSales = assignedSales, AssignedCashier = assignedCashier,
            MissingSales = Math.Max(0, reqSales - assignedSales),
            MissingCashier = Math.Max(0, reqCashier - assignedCashier),
            HasLevel2Plus = hasLevel2Plus,
            IsSatisfied = assignedSales == reqSales && assignedCashier == reqCashier
                && (hasLevel2Plus || reqSales + reqCashier == 0),
            AssignedEmployees = assignedItems
        };
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

    private static bool CanSchedulePeriod(
        SchedulePeriod period,
        WorkSchedule? workSchedule,
        DateTime now,
        DateOnly currentDate)
    {
        return period.RegistrationStatus == RegistrationStatus.Closed
            || (period.RegistrationStatus == RegistrationStatus.Open
                && !SchedulePeriodStatusHelper.IsRegistrationEffectivelyOpen(period, now, currentDate))
            || workSchedule is not null;
    }
}
