using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Features.WorkSchedules.Services;
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

    [Fact(DisplayName = "TC-BR01-CFG-01: Ba ca active khac nhau ve policy thi bi tu choi ShiftConfigurationInvalid")]
    public async Task UpdateConfigurationAsync_WhenActiveShiftsHaveDifferentPolicies_ReturnsShiftConfigurationInvalid()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        repo.ShiftTypes[0].MaxShiftsPerEmployeePerDay = 1;
        repo.ShiftTypes[1].MaxShiftsPerEmployeePerDay = 2; // Inconsistent policy
        var service = CreateService(repo, new FakeUnitOfWork(), new FakeAuditLogRepository());

        var model = CreateValidModel(1);
        model.MaxShiftsPerEmployeePerDay = 2;

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftConfigurationInvalid, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-BR01-CFG-02: Max=1 tu dong ep AllowSplitShift=false khi luu ca moi")]
    public async Task UpdateConfigurationAsync_WhenMaxIs1AndSplitIsTrue_EnforcesSplitFalse()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        repo.ShiftTypes.ForEach(s => { s.MaxShiftsPerEmployeePerDay = 2; s.AllowSplitShift = false; });
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var model = CreateValidModel(1);
        model.MaxShiftsPerEmployeePerDay = 1;
        model.AllowSplitShift = true; // User checks split shift, but Max is 1

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.True(result.Succeeded);
        var activeShifts = await repo.GetActiveShiftsAsync(true);
        Assert.Equal(3, activeShifts.Count);
        Assert.All(activeShifts, s =>
        {
            Assert.Equal(2, s.ConfigurationVersion);
            Assert.Equal(1, s.MaxShiftsPerEmployeePerDay);
            Assert.False(s.AllowSplitShift);
        });
    }

    [Fact(DisplayName = "TC-BR01-CFG-03: Max=2 cho phep luu AllowSplitShift=true")]
    public async Task UpdateConfigurationAsync_WhenMaxIs2AndSplitIsTrue_SavesBothValues()
    {
        var repo = CreateRepositoryWithThreeFixedShifts(version: 1);
        repo.ShiftTypes.ForEach(s => { s.MaxShiftsPerEmployeePerDay = 2; s.AllowSplitShift = false; });
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var service = CreateService(repo, uow, auditRepo);

        var model = CreateValidModel(1);
        model.MaxShiftsPerEmployeePerDay = 2;
        model.AllowSplitShift = true;

        var result = await service.UpdateConfigurationAsync(model, 1);

        Assert.True(result.Succeeded);
        var activeShifts = await repo.GetActiveShiftsAsync(true);
        Assert.Equal(3, activeShifts.Count);
        Assert.All(activeShifts, s =>
        {
            Assert.Equal(2, s.ConfigurationVersion);
            Assert.Equal(2, s.MaxShiftsPerEmployeePerDay);
            Assert.True(s.AllowSplitShift);
        });
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

    [Fact(DisplayName = "TC-BR01-RES-01: Resolver nhan ba ca cung version va policy -> thanh cong")]
    public void TryResolvePolicy_WhenThreeShiftsSameVersionAndPolicy_Succeeds()
    {
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false }
        };

        var ok = DailyShiftRuleEvaluator.TryResolvePolicy(shifts, out var policy, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(1, policy.MaxShifts);
        Assert.False(policy.AllowSplitShift);
    }

    [Fact(DisplayName = "TC-BR01-RES-02: Cac ca khac ConfigurationVersion -> that bai")]
    public void TryResolvePolicy_WhenShiftsHaveDifferentConfigurationVersions_Fails()
    {
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 1, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false }
        };

        var ok = DailyShiftRuleEvaluator.TryResolvePolicy(shifts, out _, out var error);

        Assert.False(ok);
        Assert.Contains("không thuộc cùng một phiên bản cấu hình", error);
    }

    [Fact(DisplayName = "TC-BR01-RES-03: Cac ca cung version nhung khac MaxShifts -> that bai")]
    public void TryResolvePolicy_WhenShiftsSameVersionButDifferentMaxShifts_Fails()
    {
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false }
        };

        var ok = DailyShiftRuleEvaluator.TryResolvePolicy(shifts, out _, out var error);

        Assert.False(ok);
        Assert.Contains("số ca tối đa/ngày", error);
    }

    [Fact(DisplayName = "TC-BR01-RES-04: Cac ca cung version nhung khac AllowSplitShift -> that bai")]
    public void TryResolvePolicy_WhenShiftsSameVersionButDifferentAllowSplitShift_Fails()
    {
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = true },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false }
        };

        var ok = DailyShiftRuleEvaluator.TryResolvePolicy(shifts, out _, out var error);

        Assert.False(ok);
        Assert.Contains("ca gãy", error);
    }

    [Fact(DisplayName = "TC-BR01-RES-05: Khong co period shifts -> that bai, khong fallback active")]
    public void TryResolvePolicy_WhenNoPeriodShifts_FailsAndDoesNotFallbackToActive()
    {
        var ok = DailyShiftRuleEvaluator.TryResolvePolicy(new List<ShiftType>(), out _, out var error);

        Assert.False(ok);
        Assert.Contains("không có dữ liệu ca làm việc hoặc định mức nhân sự", error);

        var okNull = DailyShiftRuleEvaluator.TryResolvePolicy(null, out _, out var errorNull);
        Assert.False(okNull);
        Assert.NotNull(errorNull);
    }

    [Fact(DisplayName = "TC-BR01-AUDIT-01: Thieu mot trong ba ma ca cua ky -> fail-closed")]
    public void TryResolvePolicy_WhenMissingOneOfThreeShiftCodes_Fails()
    {
        // Chỉ có MORNING và AFTERNOON (thiếu EVENING)
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false }
        };

        var ok = DailyShiftRuleEvaluator.TryResolvePolicy(shifts, out _, out var error);

        Assert.False(ok);
        Assert.Contains("phải có chính xác 3 ca cố định", error);

        // Đủ 3 ca nhưng mã sai (thiếu EVENING, có NIGHT thay thế)
        var wrongCodeShifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 3, ShiftCode = "NIGHT", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false }
        };

        var okWrong = DailyShiftRuleEvaluator.TryResolvePolicy(wrongCodeShifts, out _, out var errorWrong);

        Assert.False(okWrong);
        Assert.Contains("phải chứa đầy đủ 3 ca: MORNING, AFTERNOON và EVENING", errorWrong);
    }

    [Fact(DisplayName = "TC-BR01-AUDIT-02: StaffingRequirement chua ShiftTypeId khong ton tai trong bo ca ky -> fail-closed")]
    public void ExtractPeriodShifts_WhenStaffingRequirementReferencesUnknownShiftTypeId_FailsClosed()
    {
        var knownShifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false }
        };

        // Requirement tham chiếu ShiftTypeId = 999 không có trong knownShifts và ShiftType null
        var requirements = new List<StaffingRequirement>
        {
            new() { ShiftTypeId = 1 },
            new() { ShiftTypeId = 999 }
        };

        var extracted = DailyShiftRuleEvaluator.ExtractPeriodShifts(requirements, knownShifts);

        // Bắt buộc fail-closed, trả về rỗng thay vì bỏ qua ShiftTypeId = 999 rồi tiếp tục với ca 1
        Assert.Empty(extracted);

        var ok = DailyShiftRuleEvaluator.TryResolvePeriodPolicy(requirements, knownShifts, out _, out var error);
        Assert.False(ok);
        Assert.Contains("không tồn tại", error);
    }

    [Fact(DisplayName = "TC-BR01-AUDIT-03: Max = 1 va AllowSplitShift = true -> fail-closed")]
    public void TryResolvePolicy_WhenMaxIs1AndAllowSplitShiftIsTrue_Fails()
    {
        var shifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = true },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = true },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 2, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = true }
        };

        var ok = DailyShiftRuleEvaluator.TryResolvePolicy(shifts, out _, out var error);

        Assert.False(ok);
        Assert.Contains("không nhất quán", error);
    }

    [Fact(DisplayName = "TC-BR01-AUDIT-04: StaffingRequirement co ShiftTypeId khac ShiftType.ShiftTypeId -> fail-closed")]
    public void ExtractPeriodShifts_WhenStaffingRequirementNavigationMismatchesFk_FailsClosed()
    {
        var morning = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ConfigurationVersion = 1,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false
        };
        var afternoon = new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ConfigurationVersion = 1,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false
        };

        // Requirement có FK = 1 nhưng Navigation trỏ sang afternoon (ShiftTypeId = 2)
        var requirements = new List<StaffingRequirement>
        {
            new() { ShiftTypeId = 1, ShiftType = afternoon }
        };

        var extracted = DailyShiftRuleEvaluator.ExtractPeriodShifts(requirements, new[] { morning, afternoon });

        // Bắt buộc fail-closed: trả về rỗng, không nhận navigation sai và không fallback
        Assert.Empty(extracted);

        var ok = DailyShiftRuleEvaluator.TryResolvePeriodPolicy(requirements, new[] { morning, afternoon }, out _, out var error);
        Assert.False(ok);
        Assert.Contains("không nhất quán", error);
    }

    [Fact(DisplayName = "TC-BR01-AUDIT-05: Requirement lap ShiftTypeId nhung navigation sau khong khop FK -> fail-closed")]
    public void ExtractPeriodShifts_WhenDuplicateShiftTypeIdHasSubsequentMismatchedNavigation_FailsClosed()
    {
        var morning = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ConfigurationVersion = 1,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false
        };
        var afternoon = new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ConfigurationVersion = 1,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false
        };

        // Requirement 1: đúng FK và navigation (ShiftTypeId = 1, ShiftType.ShiftTypeId = 1)
        // Requirement 2: lặp ShiftTypeId = 1 nhưng navigation sau không khớp (ShiftType.ShiftTypeId = 2)
        var requirements = new List<StaffingRequirement>
        {
            new() { ShiftTypeId = 1, ShiftType = morning },
            new() { ShiftTypeId = 1, ShiftType = afternoon }
        };

        var extracted = DailyShiftRuleEvaluator.ExtractPeriodShifts(requirements, new[] { morning, afternoon });

        // Bắt buộc fail-closed: trả về rỗng, không được bỏ qua requirement thứ hai chỉ vì ID 1 đã có trong dictionary
        Assert.Empty(extracted);

        var ok = DailyShiftRuleEvaluator.TryResolvePeriodPolicy(requirements, new[] { morning, afternoon }, out _, out var error);
        Assert.False(ok);
        Assert.Contains("không nhất quán", error);
    }
}
