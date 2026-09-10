using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Results;

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

    Task<ServiceResult> AdjustWorkdayHoursAsync(
        int actorAccountId,
        AdjustWorkdayHoursRequest request,
        CancellationToken cancellationToken = default);
}
