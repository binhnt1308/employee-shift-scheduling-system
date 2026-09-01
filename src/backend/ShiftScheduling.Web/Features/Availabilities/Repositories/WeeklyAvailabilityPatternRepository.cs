using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Repositories;

public sealed class WeeklyAvailabilityPatternRepository : IWeeklyAvailabilityPatternRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WeeklyAvailabilityPatternRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<WeeklyAvailabilityPattern>> GetByEmployeeIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WeeklyAvailabilityPatterns
            .AsNoTracking()
            .Where(p => p.EmployeeId == employeeId)
            .OrderBy(p => p.DayOfWeek)
            .ThenBy(p => p.ShiftCode)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertByEmployeeAsync(
        int employeeId,
        IEnumerable<(int DayOfWeek, string ShiftCode, AvailabilityPreferenceLevel PreferenceLevel)> entries,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // DB-07: batch load tất cả pattern hiện tại, tránh N+1.
        var existing = await _dbContext.WeeklyAvailabilityPatterns
            .Where(p => p.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        var newEntries = entries.ToList();
        var newKeys = newEntries
            .Select(e => (e.DayOfWeek, e.ShiftCode))
            .ToHashSet();

        var existingMap = existing.ToDictionary(p => (p.DayOfWeek, p.ShiftCode));

        // Xóa các entry không còn trong danh sách mới
        foreach (var item in existing)
        {
            if (!newKeys.Contains((item.DayOfWeek, item.ShiftCode)))
            {
                _dbContext.WeeklyAvailabilityPatterns.Remove(item);
            }
        }

        foreach (var (dayOfWeek, shiftCode, preferenceLevel) in newEntries)
        {
            if (existingMap.TryGetValue((dayOfWeek, shiftCode), out var existingEntry))
            {
                // Cập nhật preference nếu thay đổi
                if (existingEntry.PreferenceLevel != preferenceLevel)
                {
                    existingEntry.PreferenceLevel = preferenceLevel;
                    existingEntry.UpdatedAt = now;
                }
            }
            else
            {
                // Thêm mới
                _dbContext.WeeklyAvailabilityPatterns.Add(new WeeklyAvailabilityPattern
                {
                    EmployeeId = employeeId,
                    DayOfWeek = dayOfWeek,
                    ShiftCode = shiftCode,
                    PreferenceLevel = preferenceLevel,
                    CreatedAt = now
                });
            }
        }
    }
}
