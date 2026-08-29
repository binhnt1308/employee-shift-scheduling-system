using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.ViewModels;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.ShiftTypes;

public sealed class ShiftConfigurationValidationUnitTests
{
    [Fact(DisplayName = "TC-SHIFT-CONFIG-001: GET tra dung ba ca active cung version")]
    public async Task GetConfigurationAsync_ReturnsThreeActiveShiftsWithMatchingVersion()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var result = await service.GetConfigurationAsync();

        Assert.NotNull(result);
        Assert.Equal(1, result!.ExpectedConfigurationVersion);
        Assert.Equal(3, result.Shifts.Count);
        Assert.Contains(result.Shifts, s => s.ShiftCode == ShiftTypeConstants.MorningCode);
        Assert.Contains(result.Shifts, s => s.ShiftCode == ShiftTypeConstants.AfternoonCode);
        Assert.Contains(result.Shifts, s => s.ShiftCode == ShiftTypeConstants.EveningCode);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-002: Luu bo ba hop le tao version moi")]
    public async Task UpdateConfigurationAsync_ValidSet_CreatesNewVersion()
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
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(17, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, actorAccountId: 1);

        Assert.True(result.Succeeded);
        var activeShifts = await repo.GetActiveShiftsAsync(true);
        Assert.Equal(3, activeShifts.Count);
        Assert.All(activeShifts, s => Assert.Equal(2, s.ConfigurationVersion));
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-003: Ban ghi version cu chuyen inactive")]
    public async Task UpdateConfigurationAsync_ValidSet_OldRecordsBecomeInactive()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = CreateValidModel(expectedVersion: 1);
        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.True(result.Succeeded);
        var allShifts = repo.ShiftTypes;
        var oldShifts = allShifts.Where(s => s.ConfigurationVersion == 1).ToList();
        Assert.Equal(3, oldShifts.Count);
        Assert.All(oldShifts, s => Assert.False(s.IsActive));
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-004: Ba ban ghi moi active va cung newVersion")]
    public async Task UpdateConfigurationAsync_ValidSet_NewRecordsAreActiveWithSameVersion()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = CreateValidModel(expectedVersion: 1);
        await service.UpdateConfigurationAsync(model, 1);

        var newShifts = repo.ShiftTypes.Where(s => s.ConfigurationVersion == 2).ToList();
        Assert.Equal(3, newShifts.Count);
        Assert.All(newShifts, s => Assert.True(s.IsActive));
        Assert.All(newShifts, s => Assert.Equal(2, s.ConfigurationVersion));
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-005: DurationHours duoc tinh dung tu gio")]
    public async Task UpdateConfigurationAsync_ComputesDurationHoursCorrectly()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(13, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(17, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(20, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.True(result.Succeeded);
        var newShifts = repo.ShiftTypes.Where(s => s.ConfigurationVersion == 2).ToList();
        var morning = newShifts.First(s => s.ShiftCode == ShiftTypeConstants.MorningCode);
        var afternoon = newShifts.First(s => s.ShiftCode == ShiftTypeConstants.AfternoonCode);
        var evening = newShifts.First(s => s.ShiftCode == ShiftTypeConstants.EveningCode);

        Assert.Equal(5.00m, morning.DurationHours);
        Assert.Equal(4.00m, afternoon.DurationHours);
        Assert.Equal(3.00m, evening.DurationHours);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-006: Thoi luong 4,5 gio duoc tinh chinh xac")]
    public async Task UpdateConfigurationAsync_FractionalDuration_ComputesAccurately()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(7, 30), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 30) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(16, 30), EndTime = new TimeOnly(21, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.True(result.Succeeded);
        var morning = repo.ShiftTypes.First(s => s.ConfigurationVersion == 2 && s.ShiftCode == ShiftTypeConstants.MorningCode);
        Assert.Equal(4.50m, morning.DurationHours);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-007: Morning chong Afternoon bi tu choi")]
    public async Task UpdateConfigurationAsync_MorningOverlapsAfternoon_ReturnsOverlapError()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(13, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTimeOverlap, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-008: Afternoon chong Evening bi tu choi")]
    public async Task UpdateConfigurationAsync_AfternoonOverlapsEvening_ReturnsOverlapError()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(17, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(16, 30), EndTime = new TimeOnly(20, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTimeOverlap, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-009: Khoang trong giua ca duoc chap nhan")]
    public async Task UpdateConfigurationAsync_GapsBetweenShifts_Succeeds()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(11, 30) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 30), EndTime = new TimeOnly(16, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-010: EndTime <= StartTime bi tu choi")]
    public async Task UpdateConfigurationAsync_EndTimeBeforeStartTime_ReturnsInvalidTimeError()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(8, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0) }
            }
        };

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTimeInvalid, result.ErrorCode);
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
