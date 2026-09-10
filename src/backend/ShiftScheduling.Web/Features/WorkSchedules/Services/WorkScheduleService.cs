using System.Globalization;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
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
        ILogger<WorkScheduleService> logger)
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
    }

    /// <inheritdoc />
    public async Task<WorkScheduleIndexViewModel> GetIndexAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var pagedPeriods = await _periodRepository.GetPagedAsync(page, pageSize, cancellationToken);
        var periodItems = new List<WorkSchedulePeriodItemViewModel>();

        foreach (var period in pagedPeriods.Items)
        {
            var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
                period.SchedulePeriodId, true, true, cancellationToken);

            var canSchedule = period.RegistrationStatus == RegistrationStatus.Closed
                || (period.RegistrationStatus == RegistrationStatus.Open
                    && period.AvailabilityDeadline <= _clock.UtcNow);

            periodItems.Add(new WorkSchedulePeriodItemViewModel
            {
                SchedulePeriodId = period.SchedulePeriodId,
                PeriodName = period.PeriodName,
                WeekStartDate = period.WeekStartDate,
                WeekEndDate = period.WeekEndDate,
                RegistrationStatus = period.RegistrationStatus,
                WorkScheduleId = schedule?.WorkScheduleId,
                ScheduleStatus = schedule?.Status,
                PublishedAt = schedule?.PublishedAt,
                TotalAssignmentsCount = schedule?.ShiftAssignments.Count ?? 0,
                CanSchedule = canSchedule || schedule is not null
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
        if (period is null)
        {
            return null;
        }

        var schedule = await _workScheduleRepository.EnsureDraftExistsAsync(
            schedulePeriodId, accountId, _clock.UtcNow, cancellationToken);

        schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, true, true, cancellationToken);

        if (schedule is null)
        {
            return null;
        }

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, null, null, cancellationToken);
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var periodShifts = period.StaffingRequirements
            .Select(r => r.ShiftType)
            .Where(st => st != null)
            .DistinctBy(st => st.ShiftTypeId)
            .OrderBy(st => st.DisplayOrder)
            .ToList();
        var fixedShifts = periodShifts.Count == 3 ? periodShifts : GetFixedThreeShifts(allShifts);

        var validationResult = _validationService.ValidateSchedule(
            period, activeEmployees, availabilities, leaves, period.StaffingRequirements.ToList(),
            schedule.ShiftAssignments.ToList(), allShifts);

        var days = BuildGridDays(
            period, fixedShifts, period.StaffingRequirements.ToList(),
            schedule.ShiftAssignments.ToList(), activeEmployees);

        return new WorkScheduleEditViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            RegistrationStatus = period.RegistrationStatus,
            WorkScheduleId = schedule.WorkScheduleId,
            Status = schedule.Status,
            IsGeneratedByORTools = schedule.IsGeneratedByORTools,
            Days = days,
            ValidationResult = validationResult
        };
    }

    private static List<SchedulingDayGridViewModel> BuildGridDays(
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        List<StaffingRequirement> requirements,
        List<ShiftAssignment> assignments,
        IReadOnlyList<Employee> activeEmployees)
    {
        var empMap = activeEmployees.ToDictionary(e => e.EmployeeId);
        var viCulture = new CultureInfo("vi-VN");
        var days = new List<SchedulingDayGridViewModel>();

        for (int i = 0; i < 7; i++)
        {
            var date = period.WeekStartDate.AddDays(i);
            var dayName = date.ToString("dddd (dd/MM)", viCulture);
            var shiftCells = new List<SchedulingShiftCellViewModel>();

            foreach (var shift in fixedShifts)
            {
                var req = requirements.FirstOrDefault(r => r.WorkDate == date && r.ShiftTypeId == shift.ShiftTypeId);
                int reqSales = req?.RequiredSalesStaff ?? 0;
                int reqCashier = req?.RequiredCashiers ?? 0;

                var shiftAssigns = assignments
                    .Where(a => a.WorkDate == date && a.ShiftTypeId == shift.ShiftTypeId)
                    .ToList();
                var assignedItems = shiftAssigns.Select(a =>
                {
                    empMap.TryGetValue(a.EmployeeId, out var emp);
                    return new AssignedEmployeeItemViewModel
                    {
                        ShiftAssignmentId = a.ShiftAssignmentId,
                        EmployeeId = a.EmployeeId,
                        EmployeeCode = emp?.EmployeeCode ?? a.Employee?.EmployeeCode ?? string.Empty,
                        FullName = emp?.FullName ?? a.Employee?.FullName ?? string.Empty,
                        Position = emp?.Position ?? a.Employee?.Position ?? EmployeePosition.Sales,
                        EmploymentType = emp?.EmploymentType ?? a.Employee?.EmploymentType ?? EmploymentType.PartTime,
                        Level = emp?.Level ?? a.Employee?.Level ?? EmployeeLevel.Level1,
                        Source = a.Source
                    };
                }).OrderBy(e => e.Position).ThenBy(e => e.FullName).ToList();

                int assignedSales = assignedItems.Count(e => e.Position == EmployeePosition.Sales);
                int assignedCashier = assignedItems.Count(e => e.Position == EmployeePosition.Cashier);
                bool hasLevel2Plus = assignedItems.Any(e => e.Level >= EmployeeLevel.Level2);

                shiftCells.Add(new SchedulingShiftCellViewModel
                {
                    WorkDate = date,
                    ShiftTypeId = shift.ShiftTypeId,
                    ShiftCode = shift.ShiftCode,
                    ShiftName = shift.ShiftName,
                    StartTime = shift.StartTime,
                    EndTime = shift.EndTime,
                    RequiredSales = reqSales,
                    RequiredCashier = reqCashier,
                    AssignedSales = assignedSales,
                    AssignedCashier = assignedCashier,
                    MissingSales = Math.Max(0, reqSales - assignedSales),
                    MissingCashier = Math.Max(0, reqCashier - assignedCashier),
                    HasLevel2Plus = hasLevel2Plus,
                    IsSatisfied = assignedSales >= reqSales
                        && assignedCashier >= reqCashier
                        && (hasLevel2Plus || reqSales + reqCashier == 0),
                    AssignedEmployees = assignedItems
                });
            }

            days.Add(new SchedulingDayGridViewModel
            {
                WorkDate = date,
                DayOfWeekName = dayName,
                Shifts = shiftCells
            });
        }

        return days;
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
}
