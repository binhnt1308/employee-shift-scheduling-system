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
}

internal sealed class FakeAvailabilityRepository : IAvailabilityRepository
{
    public List<Availability> Availabilities { get; } = [];

    public Task<List<Availability>> GetByEmployeeAndPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Availabilities.Where(
            a => a.EmployeeId == employeeId && a.SchedulePeriodId == schedulePeriodId).ToList());
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
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
