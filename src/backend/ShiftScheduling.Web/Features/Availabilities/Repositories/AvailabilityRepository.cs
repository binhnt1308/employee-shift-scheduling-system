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
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Availabilities
            .Where(a => a.EmployeeId == employeeId && a.SchedulePeriodId == schedulePeriodId)
            .ToListAsync(cancellationToken);

        var selectedSet = selectedShifts.ToHashSet();
        var existingMap = existing.ToDictionary(a => (a.WorkDate, a.ShiftTypeId));

        // Delete items no longer selected
        foreach (var item in existing)
        {
            if (!selectedSet.Contains((item.WorkDate, item.ShiftTypeId)))
            {
                _dbContext.Availabilities.Remove(item);
            }
        }

        // Add new items
        foreach (var (workDate, shiftTypeId) in selectedSet)
        {
            if (!existingMap.ContainsKey((workDate, shiftTypeId)))
            {
                _dbContext.Availabilities.Add(new Availability
                {
                    EmployeeId = employeeId,
                    SchedulePeriodId = schedulePeriodId,
                    WorkDate = workDate,
                    ShiftTypeId = shiftTypeId,
                    RegisteredAt = now
                });
            }
        }
    }
}
