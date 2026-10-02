using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Features.ShiftTypes.Controllers;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.ViewModels;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.ShiftTypes;

public sealed class ShiftConfigurationLifecycleUnitTests
{
    [Fact(DisplayName = "TC-SHIFT-CONFIG-011: Thieu mot trong ba ca bi tu choi")]
    public async Task UpdateConfigurationAsync_MissingShift_ReturnsIncompleteError()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationIncomplete, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-012: Trung ShiftCode bi tu choi")]
    public async Task UpdateConfigurationAsync_DuplicateShiftCode_ReturnsIncompleteError()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationIncomplete, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-013: ExpectedVersion cu bi tu choi")]
    public async Task UpdateConfigurationAsync_OutdatedExpectedVersion_ReturnsChangedError()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 2);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = CreateValidModel(expectedVersion: 1);
        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationChanged, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-013-B: ExpectedVersion bang 0 bi tu choi")]
    public async Task UpdateConfigurationAsync_ExpectedVersionZero_ReturnsChangedError()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var model = CreateValidModel(expectedVersion: 0);
        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationChanged, result.ErrorCode);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
        var activeShifts = await repo.GetActiveShiftsAsync(true);
        Assert.All(activeShifts, s => Assert.Equal(1, s.ConfigurationVersion));
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-014: Validation fail khong Save va khong AuditLog")]
    public async Task UpdateConfigurationAsync_WhenValidationFails_DoesNotSaveOrAudit()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(14, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-015: Update thanh cong chi goi mot SaveChanges")]
    public async Task UpdateConfigurationAsync_OnSuccess_CallsSaveChangesExactlyOnce()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var model = CreateValidModel(expectedVersion: 1);
        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-016: Tao dung mot AuditLog chua old/new set")]
    public async Task UpdateConfigurationAsync_OnSuccess_CreatesSingleAuditLog()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, new FakeUnitOfWork(), auditRepo);

        var model = CreateValidModel(expectedVersion: 1);
        var result = await service.UpdateConfigurationAsync(model, actorAccountId: 42);

        Assert.True(result.Succeeded);
        Assert.Single(auditRepo.Items);
        var log = auditRepo.Items[0];
        Assert.Equal("UPDATE_SHIFT_CONFIGURATION", log.Action);
        Assert.Equal(42, log.ActorAccountId);
        Assert.Equal(nameof(ShiftType), log.EntityName);
        Assert.Equal("2", log.EntityId);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-017: Employee khong co quyen cau hinh")]
    public void ShiftTypeController_RequiresManagerRole()
    {
        var type = typeof(ShiftTypeController);
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.Manager, attribute!.Roles);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-ACTIVE-01: Active set co 4 ca bi tu choi")]
    public async Task ActiveSet_FourShifts_GetReturnsNull_UpdateReturnsInvalid()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 4, ShiftCode = "NIGHT", DisplayOrder = 4, ConfigurationVersion = 1, IsActive = true, StartTime = new(20, 0), EndTime = new(23, 0) });
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var getResult = await service.GetConfigurationAsync();
        Assert.Null(getResult);

        var updateResult = await service.UpdateConfigurationAsync(CreateValidModel(1), 1);
        Assert.False(updateResult.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationInvalid, updateResult.ErrorCode);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-ACTIVE-02: Active set lech version bi tu choi")]
    public async Task ActiveSet_MismatchedVersions_GetReturnsNull_UpdateReturnsInvalid()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new(8, 0), EndTime = new(12, 0), DisplayOrder = 1, ConfigurationVersion = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new(12, 0), EndTime = new(16, 0), DisplayOrder = 2, ConfigurationVersion = 2, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new(16, 0), EndTime = new(20, 0), DisplayOrder = 3, ConfigurationVersion = 1, IsActive = true });

        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var getResult = await service.GetConfigurationAsync();
        Assert.Null(getResult);

        var updateResult = await service.UpdateConfigurationAsync(CreateValidModel(1), 1);
        Assert.False(updateResult.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationInvalid, updateResult.ErrorCode);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-ACTIVE-03: Active set thieu ma canonical bi tu choi")]
    public async Task ActiveSet_MissingCanonicalCode_GetReturnsNull_UpdateReturnsInvalid()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new(8, 0), EndTime = new(12, 0), DisplayOrder = 1, ConfigurationVersion = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new(12, 0), EndTime = new(16, 0), DisplayOrder = 2, ConfigurationVersion = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new(16, 0), EndTime = new(20, 0), DisplayOrder = 3, ConfigurationVersion = 1, IsActive = true });

        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var getResult = await service.GetConfigurationAsync();
        Assert.Null(getResult);

        var updateResult = await service.UpdateConfigurationAsync(CreateValidModel(1), 1);
        Assert.False(updateResult.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationInvalid, updateResult.ErrorCode);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-ACTIVE-04: Active shift co gio sai bi tu choi")]
    public async Task ActiveSet_InvalidTime_GetReturnsNull_UpdateReturnsInvalid()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new(12, 0), EndTime = new(8, 0), DisplayOrder = 1, ConfigurationVersion = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new(12, 0), EndTime = new(16, 0), DisplayOrder = 2, ConfigurationVersion = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new(16, 0), EndTime = new(20, 0), DisplayOrder = 3, ConfigurationVersion = 1, IsActive = true });

        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var getResult = await service.GetConfigurationAsync();
        Assert.Null(getResult);

        var updateResult = await service.UpdateConfigurationAsync(CreateValidModel(1), 1);
        Assert.False(updateResult.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationInvalid, updateResult.ErrorCode);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-UNCHANGED-01: Luu cau hinh giong hien hanh khong tao version moi")]
    public async Task UpdateConfigurationAsync_WhenTimesUnchanged_DoesNotCreateNewVersionAndReturnsFriendlyMessage()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        // Dữ liệu cập nhật giữ nguyên khung giờ của ba ca đang hoạt động.
        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, actorAccountId: 1);

        Assert.True(result.Succeeded);
        Assert.Equal("Cấu hình ca làm việc không có thay đổi.", result.SuccessMessage);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);

        // Repository vẫn chỉ có ba ca gốc của phiên bản 1 và chúng vẫn đang hoạt động.
        var activeShifts = await repo.GetActiveShiftsAsync(true);
        Assert.Equal(3, activeShifts.Count);
        Assert.All(activeShifts, s => Assert.Equal(1, s.ConfigurationVersion));
        Assert.All(activeShifts, s => Assert.True(s.IsActive));
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-CHANGED-01: Thay doi gio tao snapshot noi bo version moi")]
    public async Task UpdateConfigurationAsync_WhenTimesChanged_CreatesNewVersionSnapshot()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var model = CreateValidModel(expectedVersion: 1); // 7:30-12:00 etc.
        var result = await service.UpdateConfigurationAsync(model, actorAccountId: 99);

        Assert.True(result.Succeeded);
        Assert.Equal(1, uow.SaveCount);
        Assert.Single(auditRepo.Items);

        var activeShifts = await repo.GetActiveShiftsAsync(true);
        Assert.Equal(3, activeShifts.Count);
        Assert.All(activeShifts, s => Assert.Equal(2, s.ConfigurationVersion));

        // Repository có 6 ca: 3 ca phiên bản cũ đã ngừng và 3 ca phiên bản mới đang hoạt động.
        Assert.Equal(6, repo.ShiftTypes.Count);
        Assert.Equal(3, repo.ShiftTypes.Count(s => !s.IsActive && s.ConfigurationVersion == 1));
        Assert.Equal(3, repo.ShiftTypes.Count(s => s.IsActive && s.ConfigurationVersion == 2));
    }

    private static ShiftConfigurationEditViewModel CreateValidModel(int expectedVersion)
    {
        return new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = expectedVersion,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca sáng", StartTime = new TimeOnly(7, 30), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 30) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca tối", StartTime = new TimeOnly(16, 30), EndTime = new TimeOnly(21, 0) }
            }
        };
    }

    private static FakeShiftTypeRepository CreateRepositoryWithThreeFixedShifts(int version = 1)
    {
        var repository = new FakeShiftTypeRepository();
        repository.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4, DisplayOrder = 1, ConfigurationVersion = version, IsActive = true });
        repository.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DurationHours = 4, DisplayOrder = 2, ConfigurationVersion = version, IsActive = true });
        repository.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4, DisplayOrder = 3, ConfigurationVersion = version, IsActive = true });
        return repository;
    }

    private static ShiftTypeService CreateService(
        FakeShiftTypeRepository repository,
        FakeUnitOfWork unitOfWork,
        FakeAuditLogRepository auditLogRepository)
    {
        return new ShiftTypeService(repository, auditLogRepository, unitOfWork, new FakeClock(), NullLogger<ShiftTypeService>.Instance);
    }
}
