using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.Fakes;

internal sealed class FakeWorkScheduleRepository : IWorkScheduleRepository
{
    public List<WorkSchedule> Schedules { get; } = [];
    public List<ShiftAssignment> SavedAssignments { get; } = [];
    public int SaveAssignmentsCount { get; private set; }
    public int PublishScheduleCount { get; private set; }

    public Task<WorkSchedule?> GetByPeriodIdAsync(
        int schedulePeriodId,
        bool asNoTracking,
        bool includeAssignments = true,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Schedules.FirstOrDefault(s => s.SchedulePeriodId == schedulePeriodId));
    }

    public Task<WorkSchedule?> GetPublishedByPeriodIdAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Schedules.FirstOrDefault(
            s => s.SchedulePeriodId == schedulePeriodId && s.Status == ScheduleStatus.Published));
    }


    public Task<WorkSchedule> EnsureDraftExistsAsync(
        int schedulePeriodId,
        int createdByAccountId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var draft = Schedules.FirstOrDefault(s => s.SchedulePeriodId == schedulePeriodId);
        if (draft is null)
        {
            draft = new WorkSchedule
            {
                WorkScheduleId = Schedules.Count + 1,
                SchedulePeriodId = schedulePeriodId,
                Status = ScheduleStatus.Draft,
                CreatedByAccountId = createdByAccountId,
                CreatedAt = now,
                ShiftAssignments = new List<ShiftAssignment>()
            };
            Schedules.Add(draft);
        }

        return Task.FromResult(draft);
    }

    public Task SaveAssignmentsAsync(
        int workScheduleId,
        IEnumerable<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId, AssignmentSource Source)> assignments,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        SaveAssignmentsCount++;
        var schedule = Schedules.FirstOrDefault(s => s.WorkScheduleId == workScheduleId);
        if (schedule is not null)
        {
            foreach (var item in assignments)
            {
                var newAssign = new ShiftAssignment
                {
                    ShiftAssignmentId = SavedAssignments.Count + 1,
                    WorkScheduleId = workScheduleId,
                    EmployeeId = item.EmployeeId,
                    WorkDate = item.WorkDate,
                    ShiftTypeId = item.ShiftTypeId,
                    Source = item.Source,
                    WorkSchedule = schedule
                };
                SavedAssignments.Add(newAssign);
                schedule.ShiftAssignments.Add(newAssign);
            }
        }

        return Task.CompletedTask;
    }

    public Task PublishScheduleAsync(
        int workScheduleId,
        int publishedByAccountId,
        DateTime publishedAt,
        CancellationToken cancellationToken = default)
    {
        PublishScheduleCount++;
        var schedule = Schedules.FirstOrDefault(s => s.WorkScheduleId == workScheduleId);
        if (schedule is not null)
        {
            schedule.Status = ScheduleStatus.Published;
            schedule.PublishedByAccountId = publishedByAccountId;
            schedule.PublishedAt = publishedAt;
        }

        return Task.CompletedTask;
    }

    public Task<HashSet<int>> GetPublishedPeriodIdsAsync(
        IEnumerable<int> periodIds,
        CancellationToken cancellationToken = default)
    {
        var ids = periodIds.ToHashSet();
        var publishedIds = Schedules
            .Where(s => ids.Contains(s.SchedulePeriodId) && s.Status == ScheduleStatus.Published)
            .Select(s => s.SchedulePeriodId)
            .ToHashSet();
        return Task.FromResult(publishedIds);
    }

    public Task<WorkSchedule?> GetLatestPublishedScheduleAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Schedules
            .Where(s => s.Status == ScheduleStatus.Published)
            .OrderByDescending(s => s.PublishedAt)
            .ThenByDescending(s => s.WorkScheduleId)
            .FirstOrDefault());
    }

    public Task<WorkSchedule?> GetDefaultPublishedScheduleAsync(
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        var published = Schedules
            .Where(s => s.Status == ScheduleStatus.Published)
            .ToList();

        var current = published
            .Where(ws => ws.SchedulePeriod.WeekStartDate <= currentDate && currentDate <= ws.SchedulePeriod.WeekEndDate)
            .OrderByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefault();

        if (current is not null)
        {
            return Task.FromResult<WorkSchedule?>(current);
        }

        var future = published
            .Where(ws => ws.SchedulePeriod.WeekStartDate > currentDate)
            .OrderBy(ws => ws.SchedulePeriod.WeekStartDate)
            .ThenByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefault();

        if (future is not null)
        {
            return Task.FromResult<WorkSchedule?>(future);
        }

        var past = published
            .Where(ws => ws.SchedulePeriod.WeekEndDate < currentDate)
            .OrderByDescending(ws => ws.SchedulePeriod.WeekEndDate)
            .ThenByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefault();

        if (past is not null)
        {
            return Task.FromResult<WorkSchedule?>(past);
        }

        return Task.FromResult(published
            .OrderByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefault());
    }

    public Task<bool> HasFuturePublishedAssignmentAsync(
        int employeeId,
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        var hasFuture = Schedules
            .Where(ws => ws.Status == ScheduleStatus.Published)
            .SelectMany(ws => ws.ShiftAssignments)
            .Any(sa => sa.EmployeeId == employeeId && sa.WorkDate >= fromDate);

        return Task.FromResult(hasFuture);
    }
}

internal sealed class FakeAvailabilityRepository : IAvailabilityRepository
{
    public List<Availability> Availabilities { get; } = [];
    public int EmployeeAndPeriodQueryCallCount { get; private set; }
    public int BatchCountQueryCallCount { get; private set; }


    public Task<List<Availability>> GetByEmployeeAndPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        EmployeeAndPeriodQueryCallCount++;

        return Task.FromResult(Availabilities.Where(
            a => a.EmployeeId == employeeId && a.SchedulePeriodId == schedulePeriodId).ToList());
    }

    public Task<IReadOnlyDictionary<int, int>> GetCountsByEmployeeAndPeriodIdsAsync(
        int employeeId,
        IReadOnlyCollection<int> schedulePeriodIds,
        CancellationToken cancellationToken = default)
    {
        BatchCountQueryCallCount++;

        var periodIdSet = schedulePeriodIds.ToHashSet();

        IReadOnlyDictionary<int, int> counts = Availabilities
            .Where(availability =>
                availability.EmployeeId == employeeId
                && periodIdSet.Contains(availability.SchedulePeriodId))
            .GroupBy(availability => availability.SchedulePeriodId)
            .ToDictionary(group => group.Key, group => group.Count());

        return Task.FromResult(counts);
    }

    public Task<List<Availability>> GetByPeriodWithDetailsAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Availability> query = Availabilities.Where(a => a.SchedulePeriodId == schedulePeriodId);
        if (employeeIdFilter.HasValue)
        {
            query = query.Where(a => a.EmployeeId == employeeIdFilter.Value);
        }

        if (dateFilter.HasValue)
        {
            query = query.Where(a => a.WorkDate == dateFilter.Value);
        }

        if (shiftTypeIdFilter.HasValue)
        {
            query = query.Where(a => a.ShiftTypeId == shiftTypeIdFilter.Value);
        }

        return Task.FromResult(query.ToList());
    }

    public Task SaveEmployeeAvailabilitiesAsync(
        int employeeId,
        int schedulePeriodId,
        IEnumerable<(DateOnly WorkDate, int ShiftTypeId)> selectedShifts,
        IReadOnlyDictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel> preferenceMap,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        Availabilities.RemoveAll(a => a.EmployeeId == employeeId && a.SchedulePeriodId == schedulePeriodId);
        foreach (var (date, shiftId) in selectedShifts)
        {
            var pref = preferenceMap.TryGetValue((date, shiftId), out var p) ? p : AvailabilityPreferenceLevel.Normal;
            Availabilities.Add(new Availability
            {
                EmployeeId = employeeId,
                SchedulePeriodId = schedulePeriodId,
                WorkDate = date,
                ShiftTypeId = shiftId,
                PreferenceLevel = pref,
                RegisteredAt = now
            });
        }
        return Task.CompletedTask;
    }
}

internal sealed class FakeWeeklyAvailabilityPatternRepository : IWeeklyAvailabilityPatternRepository
{
    public List<WeeklyAvailabilityPattern> Patterns { get; } = new();

    public Task<List<WeeklyAvailabilityPattern>> GetByEmployeeIdAsync(
        int employeeId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Patterns.Where(p => p.EmployeeId == employeeId).ToList());
    }

    public Task UpsertByEmployeeAsync(
        int employeeId,
        IEnumerable<(int DayOfWeek, string ShiftCode, AvailabilityPreferenceLevel PreferenceLevel)> entries,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        Patterns.RemoveAll(p => p.EmployeeId == employeeId);
        foreach (var entry in entries)
        {
            Patterns.Add(new WeeklyAvailabilityPattern
            {
                EmployeeId = employeeId,
                DayOfWeek = entry.DayOfWeek,
                ShiftCode = entry.ShiftCode,
                PreferenceLevel = entry.PreferenceLevel,
                CreatedAt = now
            });
        }
        return Task.CompletedTask;
    }
}
