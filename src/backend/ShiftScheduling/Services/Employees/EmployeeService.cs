using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Data;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Services.Common;
using ShiftScheduling.ViewModels.Employee;

namespace ShiftScheduling.Services.Employees;

public sealed class EmployeeService : IEmployeeService
{
    private readonly ApplicationDbContext _dbContext;

    public EmployeeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EmployeeIndexViewModel> GetIndexAsync(
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Employees
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var searchText = keyword.Trim();
            query = query.Where(x =>
                x.EmployeeCode.Contains(searchText)
                || x.FullName.Contains(searchText)
                || (x.PhoneNumber != null && x.PhoneNumber.Contains(searchText))
                || (x.Email != null && x.Email.Contains(searchText)));
        }

        var employees = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.EmployeeCode)
            .Select(x => new EmployeeListItemViewModel
            {
                EmployeeId = x.EmployeeId,
                EmployeeCode = x.EmployeeCode,
                FullName = x.FullName,
                PhoneNumber = x.PhoneNumber,
                Position = x.Position,
                EmploymentType = x.EmploymentType,
                Level = x.Level,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return new EmployeeIndexViewModel
        {
            Keyword = keyword,
            Employees = employees
        };
    }

    public async Task<EmployeeFormViewModel?> GetFormAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => new EmployeeFormViewModel
            {
                EmployeeId = x.EmployeeId,
                EmployeeCode = x.EmployeeCode,
                FullName = x.FullName,
                DateOfBirth = x.DateOfBirth,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                Position = x.Position,
                EmploymentType = x.EmploymentType,
                Level = x.Level,
                StartDate = x.StartDate
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult> CreateAsync(
        EmployeeFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDates(model);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var normalizedCode = model.EmployeeCode.Trim().ToUpperInvariant();

        var codeExists = await _dbContext.Employees
            .AnyAsync(x => x.EmployeeCode == normalizedCode, cancellationToken);

        if (codeExists)
        {
            return ServiceResult.Failure("Mã nhân viên đã tồn tại.");
        }

        var employee = new Employee
        {
            EmployeeCode = normalizedCode,
            FullName = model.FullName.Trim(),
            DateOfBirth = model.DateOfBirth!.Value,
            PhoneNumber = NormalizeOptional(model.PhoneNumber),
            Email = NormalizeOptional(model.Email),
            Position = model.Position,
            EmploymentType = model.EmploymentType,
            Level = model.Level,
            StartDate = model.StartDate!.Value,
            IsActive = true
        };

        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateAsync(
        int employeeId,
        EmployeeFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateDates(model);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var employee = await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.EmployeeId == employeeId,
                cancellationToken);

        if (employee is null)
        {
            return ServiceResult.Failure("Không tìm thấy nhân viên.");
        }

        var normalizedCode = model.EmployeeCode.Trim().ToUpperInvariant();

        var codeExists = await _dbContext.Employees
            .AnyAsync(
                x => x.EmployeeId != employeeId
                    && x.EmployeeCode == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            return ServiceResult.Failure("Mã nhân viên đã được sử dụng.");
        }

        employee.EmployeeCode = normalizedCode;
        employee.FullName = model.FullName.Trim();
        employee.DateOfBirth = model.DateOfBirth!.Value;
        employee.PhoneNumber = NormalizeOptional(model.PhoneNumber);
        employee.Email = NormalizeOptional(model.Email);
        employee.Position = model.Position;
        employee.EmploymentType = model.EmploymentType;
        employee.Level = model.Level;
        employee.StartDate = model.StartDate!.Value;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SetActiveAsync(
        int employeeId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.EmployeeId == employeeId,
                cancellationToken);

        if (employee is null)
        {
            return ServiceResult.Failure("Không tìm thấy nhân viên.");
        }

        employee.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private static ServiceResult ValidateDates(EmployeeFormViewModel model)
    {
        if (model.DateOfBirth is null || model.StartDate is null)
        {
            return ServiceResult.Failure("Ngày sinh và ngày vào làm là bắt buộc.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        var dateOfBirth = model.DateOfBirth.Value;
        var startDate = model.StartDate.Value;

        if (dateOfBirth >= today)
        {
            return ServiceResult.Failure("Ngày sinh phải nhỏ hơn ngày hiện tại.");
        }

        if (startDate > today)
        {
            return ServiceResult.Failure("Ngày vào làm không được lớn hơn ngày hiện tại.");
        }

        if (startDate < dateOfBirth)
        {
            return ServiceResult.Failure("Ngày vào làm không thể trước ngày sinh.");
        }

        return ServiceResult.Success();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
