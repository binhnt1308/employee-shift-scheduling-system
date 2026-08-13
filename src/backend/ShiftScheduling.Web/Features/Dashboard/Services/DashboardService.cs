using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
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

    /// <summary>
    /// Khoi tao service thong ke dashboard voi cac repository tuong ung.
    /// </summary>
    public DashboardService(
        IAccountRepository accountRepository,
        IEmployeeRepository employeeRepository,
        IShiftTypeRepository shiftTypeRepository)
    {
        _accountRepository = accountRepository;
        _employeeRepository = employeeRepository;
        _shiftTypeRepository = shiftTypeRepository;
    }

    /// <summary>
    /// Truy van tong so tai khoan, nhan vien va ca lam viec tu repository.
    /// </summary>
    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetPagedAsync(null, 1, 1, cancellationToken);
        var employees = await _employeeRepository.GetPagedAsync(null, 1, 1, cancellationToken);
        var shiftTypes = await _shiftTypeRepository.GetAllAsync(asNoTracking: true, cancellationToken);

        return new DashboardViewModel
        {
            AccountCount = accounts.TotalItems,
            EmployeeCount = employees.TotalItems,
            ShiftTypeCount = shiftTypes.Count,
            ShiftTypes = shiftTypes
        };
    }
}
