using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Repositories;

public sealed class WorkScheduleRepository : IWorkScheduleRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WorkScheduleRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkSchedule?> GetByPeriodIdAsync(
        int schedulePeriodId,
        bool asNoTracking,
        bool includeAssignments = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<WorkSchedule> query = _dbContext.WorkSchedules;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        query = query.Include(w => w.SchedulePeriod)
                     .Include(w => w.CreatedByAccount)
                     .Include(w => w.PublishedByAccount);

        if (includeAssignments)
        {
            query = query.Include(w => w.ShiftAssignments)
                         .ThenInclude(a => a.Employee)
                         .Include(w => w.ShiftAssignments)
                         .ThenInclude(a => a.ShiftType);
        }

        return await query.FirstOrDefaultAsync(w => w.SchedulePeriodId == schedulePeriodId, cancellationToken);
    }

    public async Task<WorkSchedule?> GetPublishedByPeriodIdAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WorkSchedules
            .AsNoTracking()
            .Include(w => w.SchedulePeriod)
                .ThenInclude(p => p.StaffingRequirements)
                    .ThenInclude(r => r.ShiftType)
            .Include(w => w.ShiftAssignments)
            .ThenInclude(a => a.Employee)
            .Include(w => w.ShiftAssignments)
            .ThenInclude(a => a.ShiftType)
            .FirstOrDefaultAsync(w => w.SchedulePeriodId == schedulePeriodId
                && w.Status == ScheduleStatus.Published, cancellationToken);
    }

    public async Task<WorkSchedule> EnsureDraftExistsAsync(
        int schedulePeriodId,
        int createdByAccountId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.WorkSchedules
            .FirstOrDefaultAsync(w => w.SchedulePeriodId == schedulePeriodId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var period = await _dbContext.SchedulePeriods.FirstAsync(
            p => p.SchedulePeriodId == schedulePeriodId, cancellationToken);
        var workSchedule = new WorkSchedule
        {
            SchedulePeriodId = schedulePeriodId,
            ScheduleName = $"Lịch làm việc {period.PeriodName}",
            Status = ScheduleStatus.Draft,
            IsGeneratedByORTools = false,
            CreatedByAccountId = createdByAccountId,
            CreatedAt = now
        };

        _dbContext.WorkSchedules.Add(workSchedule);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return workSchedule;
    }

    public async Task SaveAssignmentsAsync(
        int workScheduleId,
        IEnumerable<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId, AssignmentSource Source)> assignments,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var existingAssignments = await _dbContext.ShiftAssignments
            .Where(a => a.WorkScheduleId == workScheduleId)
            .ToListAsync(cancellationToken);

        _dbContext.ShiftAssignments.RemoveRange(existingAssignments);

        foreach (var (employeeId, workDate, shiftTypeId, source) in assignments)
        {
            _dbContext.ShiftAssignments.Add(new ShiftAssignment
            {
                WorkScheduleId = workScheduleId,
                EmployeeId = employeeId,
                WorkDate = workDate,
                ShiftTypeId = shiftTypeId,
                Source = source,
                CreatedAt = now
            });
        }

        var schedule = await _dbContext.WorkSchedules.FirstAsync(
            w => w.WorkScheduleId == workScheduleId, cancellationToken);
        schedule.UpdatedAt = now;
    }

    public async Task PublishScheduleAsync(
        int workScheduleId,
        int publishedByAccountId,
        DateTime publishedAt,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.WorkSchedules
            .FirstAsync(w => w.WorkScheduleId == workScheduleId, cancellationToken);

        schedule.Status = ScheduleStatus.Published;
        schedule.PublishedByAccountId = publishedByAccountId;
        schedule.PublishedAt = publishedAt;
        schedule.UpdatedAt = publishedAt;

        var period = await _dbContext.SchedulePeriods
            .FirstAsync(p => p.SchedulePeriodId == schedule.SchedulePeriodId, cancellationToken);
        period.RegistrationStatus = RegistrationStatus.Closed;
    }

    public async Task<HashSet<int>> GetPublishedPeriodIdsAsync(
        IEnumerable<int> periodIds,
        CancellationToken cancellationToken = default)
    {
        var ids = periodIds.ToList();
        var publishedIds = await _dbContext.WorkSchedules
            .AsNoTracking()
            .Where(ws => ids.Contains(ws.SchedulePeriodId) && ws.Status == ScheduleStatus.Published)
            .Select(ws => ws.SchedulePeriodId)
            .ToListAsync(cancellationToken);
        return publishedIds.ToHashSet();
    }

    public Task<WorkSchedule?> GetLatestPublishedScheduleAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WorkSchedules
            .AsNoTracking()
            .Include(ws => ws.SchedulePeriod)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.ShiftType)
            .Where(ws => ws.Status == ScheduleStatus.Published)
            .OrderByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkSchedule?> GetDefaultPublishedScheduleAsync(
        DateOnly currentDate,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbContext.WorkSchedules
            .AsNoTracking()
            .Include(ws => ws.SchedulePeriod)
                .ThenInclude(p => p.StaffingRequirements)
                    .ThenInclude(r => r.ShiftType)
            .Include(ws => ws.ShiftAssignments)
                .ThenInclude(sa => sa.ShiftType)
            .Where(ws => ws.Status == ScheduleStatus.Published);

        // 1. Published schedule bao phủ ngày hiện tại (WeekStartDate <= currentDate <= WeekEndDate)
        var current = await baseQuery
            .Where(ws => ws.SchedulePeriod.WeekStartDate <= currentDate && currentDate <= ws.SchedulePeriod.WeekEndDate)
            .OrderByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is not null)
        {
            return current;
        }

        // 2. Nếu không có: Published schedule gần nhất trong tương lai (WeekStartDate > currentDate, OrderBy WeekStartDate ASC)
        var future = await baseQuery
            .Where(ws => ws.SchedulePeriod.WeekStartDate > currentDate)
            .OrderBy(ws => ws.SchedulePeriod.WeekStartDate)
            .ThenByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (future is not null)
        {
            return future;
        }

        // 3. Nếu không có: Published schedule gần nhất trong quá khứ (OrderBy WeekEndDate DESC, sau đó PublishedAt DESC)
        var past = await baseQuery
            .Where(ws => ws.SchedulePeriod.WeekEndDate < currentDate)
            .OrderByDescending(ws => ws.SchedulePeriod.WeekEndDate)
            .ThenByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (past is not null)
        {
            return past;
        }

        return await baseQuery
            .OrderByDescending(ws => ws.PublishedAt)
            .ThenByDescending(ws => ws.WorkScheduleId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasFuturePublishedAssignmentAsync(
        int employeeId,
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShiftAssignments
            .AsNoTracking()
            .AnyAsync(sa => sa.EmployeeId == employeeId
                && sa.WorkDate >= fromDate
                && sa.WorkSchedule.Status == ScheduleStatus.Published,
                cancellationToken);
    }
}
