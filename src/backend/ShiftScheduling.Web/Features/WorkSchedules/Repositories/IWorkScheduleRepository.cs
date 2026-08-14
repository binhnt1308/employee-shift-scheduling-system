using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Repositories;

public interface IWorkScheduleRepository
{
    Task<WorkSchedule?> GetByPeriodIdAsync(
        int schedulePeriodId,
        bool asNoTracking,
        bool includeAssignments = true,
        CancellationToken cancellationToken = default);

    Task<WorkSchedule?> GetPublishedByPeriodIdAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    Task<WorkSchedule> EnsureDraftExistsAsync(
        int schedulePeriodId,
        int createdByAccountId,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task SaveAssignmentsAsync(
        int workScheduleId,
        IEnumerable<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId, AssignmentSource Source)> assignments,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task PublishScheduleAsync(
        int workScheduleId,
        int publishedByAccountId,
        DateTime publishedAt,
        CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetPublishedPeriodIdsAsync(
        IEnumerable<int> periodIds,
        CancellationToken cancellationToken = default);

    Task<WorkSchedule?> GetLatestPublishedScheduleAsync(
        CancellationToken cancellationToken = default);
}
