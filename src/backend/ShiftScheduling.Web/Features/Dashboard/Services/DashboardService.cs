using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Dashboard.Services;

/// <summary>
/// Triển khai nghiệp vụ tổng hợp thống kê cho trang chủ Dashboard.
/// </summary>
public sealed partial class DashboardService : IDashboardService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly ISchedulePeriodRepository _schedulePeriodRepository;
    private readonly IClock _clock;

    public DashboardService(
        IAccountRepository accountRepository,
        IEmployeeRepository employeeRepository,
        IShiftTypeRepository shiftTypeRepository,
        ISchedulePeriodRepository schedulePeriodRepository,
        ILeaveRequestRepository leaveRequestRepository,
        IClock clock)
    {
        _accountRepository = accountRepository;
        _employeeRepository = employeeRepository;
        _shiftTypeRepository = shiftTypeRepository;
        _schedulePeriodRepository = schedulePeriodRepository;
        _leaveRequestRepository = leaveRequestRepository;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<DashboardViewModel> GetDashboardAsync(
        int? userAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetPagedAsync(
            null, 1, 1, cancellationToken);
        var employees = await _employeeRepository.GetPagedAsync(
            null, 1, 1, cancellationToken);
        var shiftTypes = await _shiftTypeRepository.GetActiveShiftsAsync(
            asNoTracking: true,
            cancellationToken);

        var currentUtc = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(
            _clock.ToVietnamTime(currentUtc));

        var overview = await BuildOverviewAsync(
            currentUtc,
            currentDate,
            cancellationToken);

        var employeeDashboard = await BuildEmployeeDashboardAsync(
            userAccountId,
            overview.TargetPeriod,
            overview.WorkSchedule,
            overview.HasSchedule,
            shiftTypes,
            currentDate,
            cancellationToken);

        var employeeStatus = BuildEmployeeStatus(
            overview.TargetPeriod,
            currentDate,
            overview.HasSchedule,
            employeeDashboard.HasMySchedule,
            overview.IsRegistrationOpen);

        return CreateDashboardViewModel(
            accounts.TotalItems,
            employees.TotalItems,
            shiftTypes,
            overview,
            employeeDashboard,
            employeeStatus);
    }
}
