using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

public interface IWorkReportService
{
    Task<ManagerWorkReportOverviewViewModel?> GetManagerReportOverviewAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        EmploymentType? employmentTypeFilter = null,
        CancellationToken cancellationToken = default);

    Task<EmployeeWorkReportOverviewViewModel?> GetEmployeeOwnReportAsync(
        int accountId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateReportCsvAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        EmploymentType? employmentTypeFilter = null,
        CancellationToken cancellationToken = default);
}
