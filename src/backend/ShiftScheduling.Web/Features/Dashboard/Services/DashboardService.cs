using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Dashboard.Services;

/// <summary>
/// Triển khai nghiệp vụ tổng hợp thống kê cho trang chủ Dashboard.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly ISchedulePeriodRepository _schedulePeriodRepository;
    private readonly IClock _clock;

    public DashboardService(
        IAccountRepository accountRepository,
        IEmployeeRepository employeeRepository,
        IShiftTypeRepository shiftTypeRepository,
        ISchedulePeriodRepository schedulePeriodRepository,
        IClock clock)
    {
        _accountRepository = accountRepository;
        _employeeRepository = employeeRepository;
        _shiftTypeRepository = shiftTypeRepository;
        _schedulePeriodRepository = schedulePeriodRepository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<DashboardViewModel> GetDashboardAsync(
        int? userAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetPagedAsync(null, 1, 1, cancellationToken);
        var employees = await _employeeRepository.GetPagedAsync(null, 1, 1, cancellationToken);
        var shiftTypes = await _shiftTypeRepository.GetActiveShiftsAsync(asNoTracking: true, cancellationToken);

        var currentUtc = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(currentUtc));

        var periods = await _schedulePeriodRepository.GetAllWithSchedulesAsync(cancellationToken);

        var targetPeriod = periods.FirstOrDefault(sp => sp.WeekStartDate <= currentDate && currentDate <= sp.WeekEndDate)
            ?? periods.Where(sp => sp.WeekStartDate > currentDate).OrderBy(sp => sp.WeekStartDate).FirstOrDefault();

        string? targetPeriodName = null;
        string? targetPeriodDateRange = null;
        string scheduleStatusDisplay = "Chưa có kỳ lịch";
        bool needsAttention = false;
        string? attentionMessage = null;
        bool hasSchedule = false;
        bool hasMySchedule = false;
        bool isRegistrationOpen = false;
        string? employeeFullName = null;
        int employeeTotalAssignedShifts = 0;
        ShiftScheduling.Models.Entities.Employee? currentEmployee = null;
        WorkSchedule? workSchedule = null;

        if (targetPeriod is not null)
        {
            targetPeriodName = targetPeriod.PeriodName;
            targetPeriodDateRange = $"{targetPeriod.WeekStartDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)} - {targetPeriod.WeekEndDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}";

            workSchedule = targetPeriod.WorkSchedules
                .OrderByDescending(ws => ws.WorkScheduleId)
                .FirstOrDefault();

            var statusResult = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
                targetPeriod, workSchedule, currentUtc, currentDate);

            scheduleStatusDisplay = statusResult.StatusName;
            needsAttention = statusResult.NeedsAttention;
            attentionMessage = statusResult.AttentionMessage;
            isRegistrationOpen = statusResult.IsRegistrationOpen;

            if (workSchedule is not null && workSchedule.Status == ScheduleStatus.Published)
            {
                hasSchedule = true;
            }
        }

        if (userAccountId.HasValue)
        {
            var account = await _accountRepository.GetByIdAsync(userAccountId.Value, includeEmployee: true, cancellationToken);
            if (account?.Employee is not null)
            {
                currentEmployee = account.Employee;
                employeeFullName = currentEmployee.FullName;
                if (hasSchedule && workSchedule is not null)
                {
                    var myAssignments = workSchedule.ShiftAssignments
                        .Where(a => a.EmployeeId == currentEmployee.EmployeeId)
                        .ToList();
                    hasMySchedule = myAssignments.Count > 0;
                    employeeTotalAssignedShifts = myAssignments.Count;
                }
            }
        }

        string employeeScheduleStatusDisplay = "Chưa có kỳ lịch";
        string employeePeriodLabel = "Chưa có kỳ lịch";
        string employeeScheduleBadgeClass = "badge-ended";

        if (targetPeriod is not null)
        {
            if (targetPeriod.WeekStartDate <= currentDate && currentDate <= targetPeriod.WeekEndDate)
            {
                employeePeriodLabel = "Kỳ lịch hiện tại";
            }
            else if (targetPeriod.WeekStartDate > currentDate)
            {
                employeePeriodLabel = "Kỳ lịch sắp tới";
            }

            if (!hasSchedule)
            {
                employeeScheduleStatusDisplay = "Lịch làm việc chưa được công bố";
                employeeScheduleBadgeClass = isRegistrationOpen ? "badge-primary-custom" : "badge-info-custom";
            }
            else if (hasMySchedule)
            {
                employeeScheduleStatusDisplay = "Đã có lịch làm việc";
                employeeScheduleBadgeClass = "badge-active";
            }
            else
            {
                employeeScheduleStatusDisplay = "Chưa có ca được phân công";
                employeeScheduleBadgeClass = "badge-info-custom";
            }
        }

        var employeeWeekSchedule = new List<EmployeeDashboardDayScheduleViewModel>();
        if (targetPeriod is not null)
        {
            var viCulture = new System.Globalization.CultureInfo("vi-VN");
            for (int i = 0; i < 7; i++)
            {
                var date = targetPeriod.WeekStartDate.AddDays(i);
                var rawDayName = date.ToString("dddd", viCulture);
                var dayOfWeekName = viCulture.TextInfo.ToTitleCase(rawDayName);
                var dateFormatted = date.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture);
                var isToday = date == currentDate;

                var dayShifts = new List<EmployeeDashboardShiftItemViewModel>();
                if (hasSchedule && currentEmployee is not null && workSchedule is not null)
                {
                    var dayAssigns = workSchedule.ShiftAssignments
                        .Where(a => a.EmployeeId == currentEmployee.EmployeeId && a.WorkDate == date)
                        .ToList();

                    foreach (var assign in dayAssigns)
                    {
                        var shiftType = shiftTypes.FirstOrDefault(st => st.ShiftTypeId == assign.ShiftTypeId)
                            ?? assign.ShiftType;
                        var shiftName = shiftType?.ShiftName ?? "Ca làm";
                        var shiftCode = shiftType?.ShiftCode ?? "";
                        var shiftHours = shiftType is not null
                            ? $"{shiftType.StartTime:HH:mm} - {shiftType.EndTime:HH:mm}"
                            : "";

                        dayShifts.Add(new EmployeeDashboardShiftItemViewModel
                        {
                            ShiftName = shiftName,
                            ShiftCode = shiftCode,
                            ShiftHours = shiftHours
                        });
                    }
                }

                employeeWeekSchedule.Add(new EmployeeDashboardDayScheduleViewModel
                {
                    Date = date,
                    DayOfWeekName = dayOfWeekName,
                    DateFormatted = dateFormatted,
                    IsToday = isToday,
                    Shifts = dayShifts
                });
            }
        }

        return new DashboardViewModel
        {
            AccountCount = accounts.TotalItems,
            EmployeeCount = employees.TotalItems,
            ShiftTypeCount = shiftTypes.Count,
            ShiftTypes = shiftTypes,
            TargetPeriodName = targetPeriodName,
            TargetPeriodDateRange = targetPeriodDateRange,
            ScheduleStatusDisplay = scheduleStatusDisplay,
            NeedsAttention = needsAttention,
            AttentionMessage = attentionMessage,
            HasSchedule = hasSchedule,
            HasMySchedule = hasMySchedule,
            EmployeeScheduleStatusDisplay = employeeScheduleStatusDisplay,
            EmployeePeriodLabel = employeePeriodLabel,
            EmployeeFullName = employeeFullName,
            IsRegistrationOpen = isRegistrationOpen,
            EmployeeScheduleBadgeClass = employeeScheduleBadgeClass,
            EmployeeWeekSchedule = employeeWeekSchedule,
            EmployeeTotalAssignedShifts = employeeTotalAssignedShifts
        };
    }
}
