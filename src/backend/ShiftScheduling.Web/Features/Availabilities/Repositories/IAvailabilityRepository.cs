using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Repositories;

public interface IAvailabilityRepository
{
    Task<List<Availability>> GetByEmployeeAndPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    Task<List<Availability>> GetByPeriodWithDetailsAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default);

    Task SaveEmployeeAvailabilitiesAsync(
        int employeeId,
        int schedulePeriodId,
        IEnumerable<(DateOnly WorkDate, int ShiftTypeId)> selectedShifts,
        DateTime now,
        CancellationToken cancellationToken = default);
}
