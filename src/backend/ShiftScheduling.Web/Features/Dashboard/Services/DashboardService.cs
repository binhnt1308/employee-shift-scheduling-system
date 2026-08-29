using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Dashboard.Services;

/// <summary>
/// Trien khai nghiep vu tong hop thong ke cho trang chu Dashboard.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly IWorkScheduleRepository _workScheduleRepository;

    /// <summary>
    /// Khoi tao service thong ke dashboard voi cac repository tuong ung.
    /// </summary>
    public DashboardService(
        IAccountRepository accountRepository,
        IEmployeeRepository employeeRepository,
        IShiftTypeRepository shiftTypeRepository,
        IWorkScheduleRepository workScheduleRepository)
    {
        _accountRepository = accountRepository;
        _employeeRepository = employeeRepository;
        _shiftTypeRepository = shiftTypeRepository;
        _workScheduleRepository = workScheduleRepository;
    }

    /// <summary>
    /// Truy van tong so tai khoan, nhan vien va ca lam viec tu repository.
    /// </summary>
    public async Task<DashboardViewModel> GetDashboardAsync(
        int? userAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetPagedAsync(null, 1, 1, cancellationToken);
        var employees = await _employeeRepository.GetPagedAsync(null, 1, 1, cancellationToken);
        var shiftTypes = await _shiftTypeRepository.GetAllAsync(asNoTracking: true, cancellationToken);

        var latestPublishedSchedule = await _workScheduleRepository.GetLatestPublishedScheduleAsync(cancellationToken);
        var hasSchedule = latestPublishedSchedule is not null;

        var hasMySchedule = false;
        if (userAccountId.HasValue && latestPublishedSchedule is not null)
        {
            var account = await _accountRepository.GetByIdAsync(userAccountId.Value, includeEmployee: true, cancellationToken);
            if (account?.Employee is not null)
            {
                var empId = account.Employee.EmployeeId;
                hasMySchedule = latestPublishedSchedule.ShiftAssignments.Any(a => a.EmployeeId == empId);
            }
        }

        return new DashboardViewModel
        {
            AccountCount = accounts.TotalItems,
            EmployeeCount = employees.TotalItems,
            ShiftTypeCount = shiftTypes.Count,
            ShiftTypes = shiftTypes,
            HasSchedule = hasSchedule,
            HasMySchedule = hasMySchedule
        };
    }
}
