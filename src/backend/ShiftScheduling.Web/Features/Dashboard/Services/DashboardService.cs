using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.ShiftTypes.Repositories;
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

        if (targetPeriod is not null)
        {
            targetPeriodName = targetPeriod.PeriodName;
            targetPeriodDateRange = $"{targetPeriod.WeekStartDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)} - {targetPeriod.WeekEndDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}";

            var workSchedule = targetPeriod.WorkSchedules
                .OrderByDescending(ws => ws.WorkScheduleId)
                .FirstOrDefault();

            var statusResult = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
                targetPeriod, workSchedule, currentUtc, currentDate);

            scheduleStatusDisplay = statusResult.StatusName;
            needsAttention = statusResult.NeedsAttention;
            attentionMessage = statusResult.AttentionMessage;

            if (workSchedule is not null && workSchedule.Status == ScheduleStatus.Published)
            {
                hasSchedule = true;
                if (userAccountId.HasValue)
                {
                    var account = await _accountRepository.GetByIdAsync(userAccountId.Value, includeEmployee: true, cancellationToken);
                    if (account?.Employee is not null)
                    {
                        var empId = account.Employee.EmployeeId;
                        hasMySchedule = workSchedule.ShiftAssignments.Any(a => a.EmployeeId == empId);
                    }
                }
            }
        }

        string employeeScheduleStatusDisplay = "Chưa có kỳ lịch";
        string employeePeriodLabel = "Chưa có kỳ lịch";
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
            }
            else if (hasMySchedule)
            {
                employeeScheduleStatusDisplay = "Đã có lịch làm việc";
            }
            else
            {
                employeeScheduleStatusDisplay = "Chưa có ca được phân công";
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
            EmployeePeriodLabel = employeePeriodLabel
        };
    }
}
