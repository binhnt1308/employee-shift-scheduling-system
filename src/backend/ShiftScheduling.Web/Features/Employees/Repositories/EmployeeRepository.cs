using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Employees.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly ApplicationDbContext _dbContext;

    public EmployeeRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<Employee>> GetPagedAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var searchText = keyword.Trim();
            query = query.Where(x =>
                x.EmployeeCode.Contains(searchText)
                || x.FullName.Contains(searchText)
                || (x.PhoneNumber != null && x.PhoneNumber.Contains(searchText))
                || (x.Email != null && x.Email.Contains(searchText)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.EmployeeCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Employee>(items, page, pageSize, totalItems);
    }

    public Task<Employee?> GetByIdAsync(
        int employeeId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Employee> query = _dbContext.Employees;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
    }

    public Task<bool> EmployeeCodeExistsAsync(
        string normalizedEmployeeCode,
        int? excludedEmployeeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Employees.AnyAsync(
            x => x.EmployeeCode == normalizedEmployeeCode
                && (!excludedEmployeeId.HasValue || x.EmployeeId != excludedEmployeeId.Value),
            cancellationToken);
    }

    public Task<bool> EmailExistsAsync(
        string normalizedEmail,
        int? excludedEmployeeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Employees.AnyAsync(
            x => x.Email == normalizedEmail
                && (!excludedEmployeeId.HasValue || x.EmployeeId != excludedEmployeeId.Value),
            cancellationToken);
    }

    public Task<List<Employee>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync(cancellationToken);
    }

    public Task<Employee?> GetByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Employee> query = _dbContext.Employees;
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(e => e.UserAccountId == accountId, cancellationToken);
    }

    public void Add(Employee employee)
    {
        _dbContext.Employees.Add(employee);
    }
}
