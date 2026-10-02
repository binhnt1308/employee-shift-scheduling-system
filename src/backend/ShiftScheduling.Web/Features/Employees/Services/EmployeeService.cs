using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Employees.Services;

public sealed class EmployeeService : IEmployeeService
{
    private const int MinPageSize = 5;
    private const int MaxPageSize = 50;

    private readonly IEmployeeRepository _employeeRepository;
    private readonly IWorkScheduleRepository _workScheduleRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<EmployeeService> _logger;

    /// <summary>
    /// Khởi tạo EmployeeService với các dependency được tiêm vào.
    /// </summary>
    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IWorkScheduleRepository workScheduleRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<EmployeeService> logger)
    {
        _employeeRepository = employeeRepository;
        _workScheduleRepository = workScheduleRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EmployeeIndexViewModel> GetIndexAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);
        var result = await _employeeRepository.GetPagedAsync(
            keyword,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        return new EmployeeIndexViewModel
        {
            Keyword = keyword,
            Employees = new PagedResult<EmployeeListItemViewModel>(
                result.Items.Select(MapListItem).ToArray(),
                result.Page,
                result.PageSize,
                result.TotalItems)
        };
    }

    /// <inheritdoc />
    public async Task<EmployeeFormViewModel?> GetFormAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, true, cancellationToken);
        return employee is null ? null : MapForm(employee);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(
        EmployeeFormViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(model, null, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var employee = BuildEmployee(model);
        _employeeRepository.Add(employee);
        AddAuditLog(actorAccountId, "CREATE_EMPLOYEE", employee, null, BuildAuditValues(employee));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee created. ActorAccountId: {ActorAccountId}; EmployeeCode: {EmployeeCode}",
            actorAccountId,
            employee.EmployeeCode);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(
        int employeeId,
        EmployeeFormViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, false, cancellationToken);
        if (employee is null)
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotFound, "Không tìm thấy nhân viên.");
        }

        var validation = await ValidateAsync(model, employeeId, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var previousValues = BuildAuditValues(employee);
        ApplyChanges(employee, model);
        AddAuditLog(actorAccountId, "UPDATE_EMPLOYEE", employee, previousValues, BuildAuditValues(employee));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee updated. ActorAccountId: {ActorAccountId}; EmployeeId: {EmployeeId}",
            actorAccountId,
            employeeId);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SetActiveAsync(
        int employeeId,
        bool isActive,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId, false, cancellationToken);
        if (employee is null)
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotFound, "Không tìm thấy nhân viên.");
        }

        if (!isActive)
        {
            var hasFuturePublished = await _workScheduleRepository.HasFuturePublishedAssignmentAsync(
                employeeId,
                _clock.TodayInVietnam,
                cancellationToken);

            if (hasFuturePublished)
            {
                return ServiceResult.Failure(
                    ErrorCodes.EmployeeHasFutureAssignments,
                    "Không thể ngừng hoạt động nhân viên vì vẫn còn ca làm từ hôm nay trở đi trong lịch đã công bố. Vui lòng điều chỉnh lịch trước.");
            }
        }

        // BR02: Không xóa hồ sơ đã có lịch sử; chỉ thay đổi trạng thái hoạt động.
        var previousValues = new { employee.IsActive };
        employee.IsActive = isActive;
        AddAuditLog(actorAccountId, "SET_EMPLOYEE_STATUS", employee, previousValues, new { employee.IsActive });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Employee status changed. ActorAccountId: {ActorAccountId}; EmployeeId: {EmployeeId}; IsActive: {IsActive}",
            actorAccountId,
            employeeId,
            isActive);
        return ServiceResult.Success();
    }

    /// <summary>
    /// Kiểm tra enum, ngày sinh/ngày vào làm và tính duy nhất của mã nhân viên, email.
    /// </summary>
    private async Task<ServiceResult> ValidateAsync(
        EmployeeFormViewModel model,
        int? excludedEmployeeId,
        CancellationToken cancellationToken)
    {
        // BR02: Mỗi nhân viên chỉ có một vị trí, một hình thức làm việc và một cấp độ hợp lệ.
        if (!Enum.IsDefined(typeof(EmployeePosition), model.Position)
            || !Enum.IsDefined(typeof(EmploymentType), model.EmploymentType)
            || !Enum.IsDefined(typeof(EmployeeLevel), model.Level))
        {
            return ServiceResult.Failure(
            ErrorCodes.InvalidEnumValue,
            "Thông tin phân loại nhân viên không hợp lệ.");
        }

        var dateValidation = ValidateDates(model);
        if (!dateValidation.Succeeded)
        {
            return dateValidation;
        }

        var employeeCode = NormalizeEmployeeCode(model.EmployeeCode);
        if (await _employeeRepository.EmployeeCodeExistsAsync(employeeCode, excludedEmployeeId, cancellationToken))
        {
            return ServiceResult.Failure(
            ErrorCodes.DuplicateEmployeeCode,
            "Mã nhân viên đã được sử dụng.");
        }

        var email = NormalizeEmail(model.Email);
        if (email is not null
            && await _employeeRepository.EmailExistsAsync(email, excludedEmployeeId, cancellationToken))
        {
            return ServiceResult.Failure(ErrorCodes.DuplicateEmployeeEmail, "Email đã được sử dụng.");
        }

        return ServiceResult.Success();
    }

    /// <summary>
    /// Kiểm tra ngày sinh phải trước ngày hiện tại và ngày vào làm phải hợp lệ.
    /// </summary>
    private ServiceResult ValidateDates(EmployeeFormViewModel model)
    {
        if (model.DateOfBirth is null || model.StartDate is null)
        {
            return ServiceResult.Failure(
            ErrorCodes.InvalidEmployeeDate,
            "Ngày sinh và ngày vào làm là bắt buộc.");
        }

        var dateOfBirth = model.DateOfBirth.Value;
        var startDate = model.StartDate.Value;
        if (dateOfBirth >= _clock.TodayInVietnam)
        {
            return ServiceResult.Failure(
            ErrorCodes.InvalidEmployeeDate,
            "Ngày sinh phải nhỏ hơn ngày hiện tại.");
        }

        if (startDate > _clock.TodayInVietnam || startDate < dateOfBirth)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidEmployeeDate,
                "Ngày vào làm không hợp lệ hoặc nằm trước ngày sinh.");
        }

        return ServiceResult.Success();
    }

    /// <summary>
    /// Khởi tạo đối tượng Employee từ ViewModel.
    /// </summary>
    private static Employee BuildEmployee(EmployeeFormViewModel model)
    {
        var employee = new Employee { IsActive = true };
        ApplyChanges(employee, model);
        return employee;
    }

    /// <summary>
    /// Cập nhật các thuộc tính của Employee từ ViewModel.
    /// </summary>
    private static void ApplyChanges(Employee employee, EmployeeFormViewModel model)
    {
        employee.EmployeeCode = NormalizeEmployeeCode(model.EmployeeCode);
        employee.FullName = model.FullName.Trim();
        employee.DateOfBirth = model.DateOfBirth!.Value;
        employee.PhoneNumber = NormalizeOptional(model.PhoneNumber);
        employee.Email = NormalizeEmail(model.Email);
        employee.Position = model.Position;
        employee.EmploymentType = model.EmploymentType;
        employee.Level = model.Level;
        employee.StartDate = model.StartDate!.Value;
    }

    /// <summary>
    /// Ghi nhật ký thao tác (Audit Log) vào hệ thống.
    /// </summary>
    private void AddAuditLog(
        int actorAccountId,
        string action,
        Employee employee,
        object? previousValues,
        object? newValues)
    {
        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            action,
            nameof(Employee),
            employee.EmployeeId == 0 ? employee.EmployeeCode : employee.EmployeeId.ToString(),
            previousValues,
            newValues,
            _clock.UtcNow));
    }

    /// <summary>
    /// Xây dựng dữ liệu giá trị để lưu nhật ký thay đổi.
    /// </summary>
    private static object BuildAuditValues(Employee employee)
    {
        return new
        {
            employee.EmployeeCode,
            employee.FullName,
            employee.Email,
            employee.Position,
            employee.EmploymentType,
            employee.Level,
            employee.StartDate,
            employee.IsActive
        };
    }

    /// <summary>
    /// Chuyển đổi Entity Employee sang EmployeeFormViewModel.
    /// </summary>
    private static EmployeeFormViewModel MapForm(Employee employee)
    {
        return new EmployeeFormViewModel
        {
            EmployeeId = employee.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            DateOfBirth = employee.DateOfBirth,
            PhoneNumber = employee.PhoneNumber,
            Email = employee.Email,
            Position = employee.Position,
            EmploymentType = employee.EmploymentType,
            Level = employee.Level,
            StartDate = employee.StartDate
        };
    }

    /// <summary>
    /// Chuyển đổi Entity Employee sang EmployeeListItemViewModel.
    /// </summary>
    private static EmployeeListItemViewModel MapListItem(Employee employee)
    {
        return new EmployeeListItemViewModel
        {
            EmployeeId = employee.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            PhoneNumber = employee.PhoneNumber,
            Email = employee.Email,
            Position = employee.Position,
            EmploymentType = employee.EmploymentType,
            Level = employee.Level,
            IsActive = employee.IsActive
        };
    }

    /// <summary>
    /// Chuẩn hóa mã nhân viên: viết hoa và loại bỏ khoảng trắng thừa.
    /// </summary>
    private static string NormalizeEmployeeCode(string employeeCode)
    {
        return employeeCode.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Chuẩn hóa email: viết thường và loại bỏ khoảng trắng thừa.
    /// </summary>
    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Chuẩn hóa chuỗi không bắt buộc; giá trị rỗng được quy về null.
    /// </summary>
    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
