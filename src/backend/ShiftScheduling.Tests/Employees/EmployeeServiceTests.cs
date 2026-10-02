using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Employees.Services;
using ShiftScheduling.Models.Entities;
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
        var repository = new FakeEmployeeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var auditRepository = new FakeAuditLogRepository();
        var service = CreateService(repository, unitOfWork, auditRepository);
        var model = CreateValidModel();

        var result = await service.CreateAsync(model, 1);

        Assert.True(result.Succeeded);
        Assert.Single(repository.Employees);
        Assert.Equal("NV001", repository.Employees[0].EmployeeCode);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(auditRepository.Items);
    }

    [Fact(DisplayName = "TC02-02 - UC02 BR02 - Tạo nhân viên trùng mã nhân viên")]
    public async Task CreateAsync_WhenEmployeeCodeExists_ReturnsDuplicateError()
    {
        var repository = new FakeEmployeeRepository { EmployeeCodeExists = true };
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var result = await service.CreateAsync(CreateValidModel(), 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.DuplicateEmployeeCode, result.ErrorCode);
        Assert.Empty(repository.Employees);
    }

    [Fact(DisplayName = "TC02-03 - UC02 BR02 - Tạo nhân viên có ngày vào làm trước ngày sinh")]
    public async Task CreateAsync_WhenStartDateIsBeforeBirthDate_ReturnsDateError()
    {
        var repository = new FakeEmployeeRepository();
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());
        var model = CreateValidModel();
        model.StartDate = new DateOnly(2000, 1, 1);
        model.DateOfBirth = new DateOnly(2001, 1, 1);

        var result = await service.CreateAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidEmployeeDate, result.ErrorCode);
    }

    [Fact(DisplayName = "TC02-04 - UC02 BR02 - Tạo nhân viên trùng email")]
    public async Task CreateAsync_WhenEmailExists_ReturnsDuplicateEmailError()
    {
        var repository = new FakeEmployeeRepository { EmailExists = true };
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var result = await service.CreateAsync(CreateValidModel(), 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.DuplicateEmployeeEmail, result.ErrorCode);
    }

    private static EmployeeService CreateService(
        FakeEmployeeRepository repository,
        FakeUnitOfWork unitOfWork,
        FakeAuditLogRepository auditRepository,
        FakeWorkScheduleRepository? workScheduleRepository = null,
        FakeClock? clock = null)
    {
        return new EmployeeService(
            repository,
            workScheduleRepository ?? new FakeWorkScheduleRepository(),
            auditRepository,
            unitOfWork,
            clock ?? new FakeClock(),
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

    [Fact(DisplayName = "TC02-05 - Employee reactivation persists IsActive = true")]
    public async Task SetActiveAsync_WhenReactivatingInactiveEmployee_SetsIsActiveTrue()
    {
        var repository = new FakeEmployeeRepository();
        var employee = new Employee { EmployeeId = 1, EmployeeCode = "NV001", IsActive = false };
        repository.Employees.Add(employee);
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var result = await service.SetActiveAsync(1, true, 1);

        Assert.True(result.Succeeded);
        Assert.True(employee.IsActive);
    }

    [Fact(DisplayName = "TC02-06 - Employee reactivation does not change UserAccount IsLocked")]
    public async Task SetActiveAsync_WhenReactivatingEmployee_UserAccountIsLockedUnchanged()
    {
        var repository = new FakeEmployeeRepository();
        var account = new UserAccount { UserAccountId = 10, Username = "test", IsLocked = true };
        var employee = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV001",
            IsActive = false,
            UserAccount = account,
            UserAccountId = 10
        };
        repository.Employees.Add(employee);
        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var result = await service.SetActiveAsync(1, true, 1);

        Assert.True(result.Succeeded);
        Assert.True(employee.IsActive);
        Assert.True(account.IsLocked);
    }

    [Fact(DisplayName = "TC02-07 - Deactivation blocked when future Published assignment exists")]
    public async Task SetActiveAsync_WhenFuturePublishedAssignmentExists_RejectsDeactivation()
    {
        var repository = new FakeEmployeeRepository();
        var workRepo = new FakeWorkScheduleRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) };
        var today = clock.TodayInVietnam;

        var employee = new Employee { EmployeeId = 1, EmployeeCode = "NV001", IsActive = true };
        repository.Employees.Add(employee);

        var schedule = new WorkSchedule
        {
            WorkScheduleId = 100,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new ShiftAssignment { EmployeeId = 1, WorkDate = today.AddDays(2) }
            }
        };
        workRepo.Schedules.Add(schedule);

        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository(), workRepo, clock);

        var result = await service.SetActiveAsync(1, false, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.EmployeeHasFutureAssignments, result.ErrorCode);
        Assert.True(employee.IsActive);
        Assert.Equal(ScheduleStatus.Published, schedule.Status);
        Assert.Single(schedule.ShiftAssignments);
        Assert.Equal(1, schedule.ShiftAssignments.Single().EmployeeId);
    }

    [Fact(DisplayName = "TC02-08 - Deactivation blocked when assignment is today in Published schedule")]
    public async Task SetActiveAsync_WhenAssignmentIsTodayInPublishedSchedule_RejectsDeactivation()
    {
        var repository = new FakeEmployeeRepository();
        var workRepo = new FakeWorkScheduleRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) };
        var today = clock.TodayInVietnam;

        var employee = new Employee { EmployeeId = 1, EmployeeCode = "NV001", IsActive = true };
        repository.Employees.Add(employee);

        var schedule = new WorkSchedule
        {
            WorkScheduleId = 100,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new ShiftAssignment { EmployeeId = 1, WorkDate = today }
            }
        };
        workRepo.Schedules.Add(schedule);

        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository(), workRepo, clock);

        var result = await service.SetActiveAsync(1, false, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.EmployeeHasFutureAssignments, result.ErrorCode);
        Assert.True(employee.IsActive);
        Assert.Single(schedule.ShiftAssignments);
    }

    [Fact(DisplayName = "TC02-09 - Deactivation allowed when Published assignment is in the past")]
    public async Task SetActiveAsync_WhenPublishedAssignmentIsInPast_AllowsDeactivation()
    {
        var repository = new FakeEmployeeRepository();
        var workRepo = new FakeWorkScheduleRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) };
        var today = clock.TodayInVietnam;

        var employee = new Employee { EmployeeId = 1, EmployeeCode = "NV001", IsActive = true };
        repository.Employees.Add(employee);

        var schedule = new WorkSchedule
        {
            WorkScheduleId = 100,
            Status = ScheduleStatus.Published,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new ShiftAssignment { EmployeeId = 1, WorkDate = today.AddDays(-1) }
            }
        };
        workRepo.Schedules.Add(schedule);

        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository(), workRepo, clock);

        var result = await service.SetActiveAsync(1, false, 1);

        Assert.True(result.Succeeded);
        Assert.False(employee.IsActive);
        Assert.Single(schedule.ShiftAssignments);
    }

    [Fact(DisplayName = "TC02-10 - Deactivation allowed when future assignment is in Draft schedule")]
    public async Task SetActiveAsync_WhenFutureAssignmentIsInDraftSchedule_AllowsDeactivation()
    {
        var repository = new FakeEmployeeRepository();
        var workRepo = new FakeWorkScheduleRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) };
        var today = clock.TodayInVietnam;

        var employee = new Employee { EmployeeId = 1, EmployeeCode = "NV001", IsActive = true };
        repository.Employees.Add(employee);

        var schedule = new WorkSchedule
        {
            WorkScheduleId = 100,
            Status = ScheduleStatus.Draft,
            ShiftAssignments = new List<ShiftAssignment>
            {
                new ShiftAssignment { EmployeeId = 1, WorkDate = today.AddDays(3) }
            }
        };
        workRepo.Schedules.Add(schedule);

        var service = CreateService(repository, new FakeUnitOfWork(), new FakeAuditLogRepository(), workRepo, clock);

        var result = await service.SetActiveAsync(1, false, 1);

        Assert.True(result.Succeeded);
        Assert.False(employee.IsActive);
        Assert.Single(schedule.ShiftAssignments);
    }
}
