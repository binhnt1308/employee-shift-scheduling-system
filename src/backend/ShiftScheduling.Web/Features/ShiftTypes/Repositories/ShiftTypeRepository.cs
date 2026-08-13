using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.ShiftTypes.Repositories;

public sealed class ShiftTypeRepository : IShiftTypeRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ShiftTypeRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ShiftType>> GetAllAsync(
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ShiftType> query = _dbContext.ShiftTypes;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
    }

    public Task<ShiftType?> GetByIdAsync(
        int shiftTypeId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ShiftType> query = _dbContext.ShiftTypes;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(x => x.ShiftTypeId == shiftTypeId, cancellationToken);
    }

    public Task<bool> DisplayOrderExistsAsync(
        int displayOrder,
        int excludedShiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShiftTypes.AnyAsync(
            x => x.ShiftTypeId != excludedShiftTypeId && x.DisplayOrder == displayOrder,
            cancellationToken);
    }

    public Task<bool> OverlapsAsync(
        TimeOnly startTime,
        TimeOnly endTime,
        int excludedShiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShiftTypes.AnyAsync(
            x => x.ShiftTypeId != excludedShiftTypeId
                && x.IsActive
                && startTime < x.EndTime
                && x.StartTime < endTime,
            cancellationToken);
    }
}
