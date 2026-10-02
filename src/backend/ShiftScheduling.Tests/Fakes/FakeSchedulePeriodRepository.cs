using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Tests.Fakes;

// Fake repository kỳ lịch dùng dữ liệu trong bộ nhớ để unit test Service độc lập với EF Core.
public sealed class FakeSchedulePeriodRepository : ISchedulePeriodRepository
{
    public List<SchedulePeriod> SchedulePeriods { get; } = new();
    public List<StaffingRequirement> Requirements { get; } = new();
    public bool Overlaps { get; set; }

    public Task<PagedResult<SchedulePeriod>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = SchedulePeriods.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<SchedulePeriod>(items, SchedulePeriods.Count, page, pageSize));
    }

    public Task<SchedulePeriod?> GetByIdAsync(
        int schedulePeriodId,
        bool asNoTracking,
        bool includeRequirements = false,
        CancellationToken cancellationToken = default)
    {
        var period = SchedulePeriods.FirstOrDefault(sp => sp.SchedulePeriodId == schedulePeriodId);
        if (period != null && includeRequirements && !period.StaffingRequirements.Any())
        {
            period.StaffingRequirements = Requirements.Where(r => r.SchedulePeriodId == schedulePeriodId).ToList();
        }
        return Task.FromResult(period);
    }

    public Task<IReadOnlyList<SchedulePeriod>> GetAllWithSchedulesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchedulePeriod> result = SchedulePeriods
            .OrderByDescending(sp => sp.WeekStartDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> HasOverlappingPeriodAsync(
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        int? excludedPeriodId,
        CancellationToken cancellationToken = default)
    {
        if (Overlaps)
        {
            return Task.FromResult(true);
        }

        var hasOverlap = SchedulePeriods.Any(sp =>
            (!excludedPeriodId.HasValue || sp.SchedulePeriodId != excludedPeriodId.Value) &&
            sp.WeekStartDate <= weekEndDate &&
            sp.WeekEndDate >= weekStartDate);

        return Task.FromResult(hasOverlap);
    }

    public void Add(SchedulePeriod period)
    {
        if (period.SchedulePeriodId == 0)
        {
            period.SchedulePeriodId = SchedulePeriods.Count + 1;
        }
        SchedulePeriods.Add(period);
    }

    public void AddRequirement(StaffingRequirement requirement)
    {
        if (requirement.StaffingRequirementId == 0)
        {
            requirement.StaffingRequirementId = Requirements.Count + 1;
        }
        if (requirement.SchedulePeriodId == 0 && requirement.SchedulePeriod != null)
        {
            requirement.SchedulePeriodId = requirement.SchedulePeriod.SchedulePeriodId;
        }
        Requirements.Add(requirement);
        if (requirement.SchedulePeriod != null
            && !requirement.SchedulePeriod.StaffingRequirements.Contains(requirement))
        {
            requirement.SchedulePeriod.StaffingRequirements.Add(requirement);
        }
    }
}
