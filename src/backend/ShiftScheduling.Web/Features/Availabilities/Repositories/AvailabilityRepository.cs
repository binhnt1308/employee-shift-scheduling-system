using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Repositories;

public sealed class AvailabilityRepository : IAvailabilityRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AvailabilityRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Availability>> GetByEmployeeAndPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Availabilities
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.SchedulePeriodId == schedulePeriodId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetCountsByEmployeeAndPeriodIdsAsync(
        int employeeId,
        IReadOnlyCollection<int> schedulePeriodIds,
        CancellationToken cancellationToken = default)
    {
        var periodIds = schedulePeriodIds
            .Distinct()
            .ToArray();

        if (periodIds.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _dbContext.Availabilities
            .AsNoTracking()
            .Where(availability =>
                availability.EmployeeId == employeeId
                && periodIds.Contains(availability.SchedulePeriodId))
            .GroupBy(availability => availability.SchedulePeriodId)
            .Select(group => new
            {
                SchedulePeriodId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(
                item => item.SchedulePeriodId,
                item => item.Count,
                cancellationToken);
    }

    public async Task<List<Availability>> GetByPeriodWithDetailsAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Availabilities
            .AsNoTracking()
            .Include(a => a.Employee)
            .Include(a => a.ShiftType)
            .Where(a => a.SchedulePeriodId == schedulePeriodId && a.Employee.IsActive);

        if (employeeIdFilter.HasValue && employeeIdFilter.Value > 0)
        {
            query = query.Where(a => a.EmployeeId == employeeIdFilter.Value);
        }

        if (positionFilter.HasValue)
        {
            query = query.Where(a => a.Employee.Position == positionFilter.Value);
        }

        if (dateFilter.HasValue)
        {
            query = query.Where(a => a.WorkDate == dateFilter.Value);
        }

        if (shiftTypeIdFilter.HasValue && shiftTypeIdFilter.Value > 0)
        {
            query = query.Where(a => a.ShiftTypeId == shiftTypeIdFilter.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task SaveEmployeeAvailabilitiesAsync(
        int employeeId,
        int schedulePeriodId,
        IEnumerable<(DateOnly WorkDate, int ShiftTypeId)> selectedShifts,
        IReadOnlyDictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel> preferenceMap,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Availabilities
            .Where(availability =>
                availability.EmployeeId == employeeId
                && availability.SchedulePeriodId == schedulePeriodId)
            .ToListAsync(cancellationToken);

        var selectedSet = selectedShifts.ToHashSet();
        var existingMap = existing.ToDictionary(
            availability => (availability.WorkDate, availability.ShiftTypeId));

        RemoveDeselectedAvailabilities(existing, selectedSet);
        UpsertSelectedAvailabilities(
            employeeId,
            schedulePeriodId,
            selectedSet,
            existingMap,
            preferenceMap,
            now);
    }

    private void RemoveDeselectedAvailabilities(
        IEnumerable<Availability> existing,
        IReadOnlySet<(DateOnly WorkDate, int ShiftTypeId)> selectedSet)
    {
        foreach (var availability in existing)
        {
            if (!selectedSet.Contains(
                    (availability.WorkDate, availability.ShiftTypeId)))
            {
                _dbContext.Availabilities.Remove(availability);
            }
        }
    }

    private void UpsertSelectedAvailabilities(
        int employeeId,
        int schedulePeriodId,
        IReadOnlySet<(DateOnly WorkDate, int ShiftTypeId)> selectedSet,
        IReadOnlyDictionary<(DateOnly WorkDate, int ShiftTypeId), Availability> existingMap,
        IReadOnlyDictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel> preferenceMap,
        DateTime now)
    {
        foreach (var selectedShift in selectedSet)
        {
            var level = preferenceMap.TryGetValue(selectedShift, out var preference)
                ? preference
                : AvailabilityPreferenceLevel.Normal;

            if (existingMap.TryGetValue(selectedShift, out var existingEntry))
            {
                if (existingEntry.PreferenceLevel != level)
                {
                    existingEntry.PreferenceLevel = level;
                    existingEntry.UpdatedAt = now;
                }

                continue;
            }

            _dbContext.Availabilities.Add(new Availability
            {
                EmployeeId = employeeId,
                SchedulePeriodId = schedulePeriodId,
                WorkDate = selectedShift.WorkDate,
                ShiftTypeId = selectedShift.ShiftTypeId,
                PreferenceLevel = level,
                RegisteredAt = now
            });
        }
    }
}
