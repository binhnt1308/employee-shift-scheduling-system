using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.SchedulePeriods.Repositories;

public interface ISchedulePeriodRepository
{
    Task<PagedResult<SchedulePeriod>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SchedulePeriod?> GetByIdAsync(
        int schedulePeriodId,
        bool asNoTracking,
        bool includeRequirements = false,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingPeriodAsync(
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        int? excludedPeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchedulePeriod>> GetAllWithSchedulesAsync(
        CancellationToken cancellationToken = default);

    void Add(SchedulePeriod period);

    void AddRequirement(StaffingRequirement requirement);
}
