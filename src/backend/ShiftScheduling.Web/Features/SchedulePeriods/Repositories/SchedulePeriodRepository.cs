using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.SchedulePeriods.Repositories;

public sealed class SchedulePeriodRepository : ISchedulePeriodRepository
{
    private readonly ApplicationDbContext _dbContext;

    public SchedulePeriodRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<SchedulePeriod>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.SchedulePeriods
            .AsNoTracking()
            .Include(sp => sp.CreatedByAccount);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(sp => sp.WeekStartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SchedulePeriod>(items, totalItems, page, pageSize);
    }

    public async Task<SchedulePeriod?> GetByIdAsync(
        int schedulePeriodId,
        bool asNoTracking,
        bool includeRequirements = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SchedulePeriod> query = _dbContext.SchedulePeriods;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        query = query.Include(sp => sp.CreatedByAccount);

        if (includeRequirements)
        {
            query = query
                .Include(sp => sp.StaffingRequirements)
                .ThenInclude(sr => sr.ShiftType);
        }

        return await query.FirstOrDefaultAsync(sp => sp.SchedulePeriodId == schedulePeriodId, cancellationToken);
    }

    public async Task<bool> HasOverlappingPeriodAsync(
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        int? excludedPeriodId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SchedulePeriods
            .AsNoTracking()
            .AnyAsync(sp =>
                (!excludedPeriodId.HasValue || sp.SchedulePeriodId != excludedPeriodId.Value) &&
                sp.WeekStartDate <= weekEndDate &&
                sp.WeekEndDate >= weekStartDate,
                cancellationToken);
    }

    public void Add(SchedulePeriod period)
    {
        _dbContext.SchedulePeriods.Add(period);
    }

    public void AddRequirement(StaffingRequirement requirement)
    {
        _dbContext.StaffingRequirements.Add(requirement);
    }
}
