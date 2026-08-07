using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Availabilities.Services;

public interface IAvailabilityService
{
    Task<List<AvailabilityPeriodItemViewModel>> GetEmployeePeriodsAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    Task<AvailabilityRegisterViewModel?> GetEmployeeRegisterFormAsync(
        int accountId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SaveEmployeeAvailabilityAsync(
        int accountId,
        int schedulePeriodId,
        List<string> selectedShiftKeys,
        CancellationToken cancellationToken = default);

    Task<ManagerAvailabilityOverviewViewModel?> GetManagerOverviewAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default);
}
