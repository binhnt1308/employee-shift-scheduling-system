using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Employees.Services;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.ViewModels;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Employees;

public sealed class EmployeeServiceTests
{
    [Fact(DisplayName = "TC02-01 - UC02 BR02 - Tạo nhân viên hợp lệ")]
    public async Task CreateAsync_WhenDataIsValid_CreatesEmployeeAndAuditLog()
    {
        // Sap xep
        var repository = new FakeEmployeeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var auditRepository = new FakeAuditLogRepository();
        var service = CreateService(repository, unitOfWork, auditRepository);
        var model = CreateValidModel();

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.True(result.Succeeded);
        Assert.Single(repository.Employees);
        Assert.Equal("NV001", repository.Employees[0].EmployeeCode);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(auditRepository.Items);
    }

    [Fact(DisplayName = "TC02-02 - UC02 BR02 - Tạo nhân viên trùng mã nhân viên")]
    public async Task CreateAsync_WhenEmployeeCodeExists_ReturnsDuplicateError()
    {
        // Sap xep
        var repository = new FakeEmployeeRepository { EmployeeCodeExists = true };
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());

        // Thuc hien
        var result = await service.CreateAsync(CreateValidModel(), 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.DuplicateEmployeeCode, result.ErrorCode);
        Assert.Empty(repository.Employees);
    }

    [Fact(DisplayName = "TC02-03 - UC02 BR02 - Tạo nhân viên có ngày vào làm trước ngày sinh")]
    public async Task CreateAsync_WhenStartDateIsBeforeBirthDate_ReturnsDateError()
    {
        // Sap xep
        var repository = new FakeEmployeeRepository();
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());
        var model = CreateValidModel();
        model.StartDate = new DateOnly(2000, 1, 1);
        model.DateOfBirth = new DateOnly(2001, 1, 1);

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidEmployeeDate, result.ErrorCode);
    }

    [Fact(DisplayName = "TC02-04 - UC02 BR02 - Tạo nhân viên trùng email")]
    public async Task CreateAsync_WhenEmailExists_ReturnsDuplicateEmailError()
    {
        // Sap xep
        var repository = new FakeEmployeeRepository { EmailExists = true };
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());

        // Thuc hien
        var result = await service.CreateAsync(CreateValidModel(), 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.DuplicateEmployeeEmail, result.ErrorCode);
    }

    private static EmployeeService CreateService(
        FakeEmployeeRepository repository,
        FakeUnitOfWork unitOfWork,
        FakeAuditLogRepository auditRepository)
    {
        return new EmployeeService(
            repository,
            auditRepository,
            unitOfWork,
            new FakeClock(),
            NullLogger<EmployeeService>.Instance);
    }

    private static EmployeeFormViewModel CreateValidModel()
    {
        return new EmployeeFormViewModel
        {
            EmployeeCode = "nv001",
            FullName = "Nguyễn Văn An",
            DateOfBirth = new DateOnly(2000, 1, 1),
            Email = "an@example.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level1,
            StartDate = new DateOnly(2025, 1, 1)
        };
    }
}
