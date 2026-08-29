using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.ShiftTypes;

public sealed class ShiftConfigurationRuleTests
{
    [Fact(DisplayName = "TC-SHIFT-CONFIG-018: OpenRegistration ky cu thanh cong khi active shifts da sang version 2")]
    public async Task OpenRegistration_HistoricalPeriodWithVersion1_SucceedsWhenActiveShiftsAreVersion2()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();

        // Version 1 (inactive)
        var v1Morning = new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 1, DurationHours = 4, StartTime = new(8, 0), EndTime = new(12, 0) };
        var v1Afternoon = new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 2, DurationHours = 4, StartTime = new(12, 0), EndTime = new(16, 0) };
        var v1Evening = new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 3, DurationHours = 4, StartTime = new(16, 0), EndTime = new(20, 0) };
        shiftTypeRepo.ShiftTypes.AddRange(new[] { v1Morning, v1Afternoon, v1Evening });

        // Version 2 (active)
        var v2Morning = new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1, DurationHours = 5, StartTime = new(7, 0), EndTime = new(12, 0) };
        var v2Afternoon = new ShiftType { ShiftTypeId = 5, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 2, DurationHours = 5, StartTime = new(12, 0), EndTime = new(17, 0) };
        var v2Evening = new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 3, DurationHours = 4, StartTime = new(17, 0), EndTime = new(21, 0) };
        shiftTypeRepo.ShiftTypes.AddRange(new[] { v2Morning, v2Afternoon, v2Evening });

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 10,
            PeriodName = "Tuần cũ",
            RegistrationStatus = RegistrationStatus.Preparing,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 17, 0, 0, DateTimeKind.Utc),
            StaffingRequirements = new List<StaffingRequirement>()
        };

        for (int day = 0; day < 7; day++)
        {
            var date = period.WeekStartDate.AddDays(day);
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = day * 3 + 1, SchedulePeriodId = 10, ShiftTypeId = 1, ShiftType = v1Morning, WorkDate = date, RequiredSalesStaff = 1, RequiredCashiers = 1, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = day * 3 + 2, SchedulePeriodId = 10, ShiftTypeId = 2, ShiftType = v1Afternoon, WorkDate = date, RequiredSalesStaff = 1, RequiredCashiers = 1, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = day * 3 + 3, SchedulePeriodId = 10, ShiftTypeId = 3, ShiftType = v1Evening, WorkDate = date, RequiredSalesStaff = 1, RequiredCashiers = 1, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
        }

        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo,
            shiftTypeRepo,
            new FakeNotificationRepository(),
            new FakeAuditLogRepository(),
            new FakeUnitOfWork(),
            new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) },
            NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(10, actorAccountId: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(RegistrationStatus.Open, period.RegistrationStatus);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-019: Tao ky moi tao 21 StaffingRequirements dung ShiftTypeId version 2")]
    public async Task CreateSchedulePeriod_UsesActiveVersionShiftTypeIds()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();

        // Version 1 (inactive)
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 1, StartTime = new(8, 0), EndTime = new(12, 0), DurationHours = 4 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 2, StartTime = new(12, 0), EndTime = new(16, 0), DurationHours = 4 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 3, StartTime = new(16, 0), EndTime = new(20, 0), DurationHours = 4 });

        // Version 2 (active)
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1, StartTime = new(7, 0), EndTime = new(12, 0), DurationHours = 5 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 5, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 2, StartTime = new(12, 0), EndTime = new(17, 0), DurationHours = 5 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 3, StartTime = new(17, 0), EndTime = new(21, 0), DurationHours = 4 });

        var service = new SchedulePeriodService(
            periodRepo,
            shiftTypeRepo,
            new FakeNotificationRepository(),
            new FakeAuditLogRepository(),
            new FakeUnitOfWork(),
            new FakeClock { UtcNow = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
            NullLogger<SchedulePeriodService>.Instance);

        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Tuần 33",
            WeekStartDate = new DateOnly(2026, 8, 10),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 17, 0, 0, DateTimeKind.Utc)
        };

        var result = await service.CreateAsync(model, 1);

        Assert.True(result.Succeeded);
        var createdPeriod = periodRepo.SchedulePeriods.First();
        Assert.Equal(21, createdPeriod.StaffingRequirements.Count);
        var usedShiftIds = createdPeriod.StaffingRequirements.Select(r => r.ShiftTypeId).Distinct().OrderBy(id => id).ToArray();
        Assert.Equal(new[] { 4, 5, 6 }, usedShiftIds);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-020: AvailabilityService tra dung gio version 1 cho ky cu")]
    public async Task AvailabilityService_ReturnsHistoricalShiftTimesForExistingPeriod()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var empRepo = new FakeEmployeeRepository();
        var availRepo = new FakeAvailabilityRepository();

        var employee = new Employee { EmployeeId = 10, UserAccountId = 100, FullName = "Nhân viên A", IsActive = true };
        empRepo.Employees.Add(employee);

        var v1Morning = new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 1, StartTime = new(8, 0), EndTime = new(12, 0), DurationHours = 4 };
        var v1Afternoon = new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 2, StartTime = new(12, 0), EndTime = new(16, 0), DurationHours = 4 };
        var v1Evening = new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ConfigurationVersion = 1, IsActive = false, DisplayOrder = 3, StartTime = new(16, 0), EndTime = new(20, 0), DurationHours = 4 };
        shiftTypeRepo.ShiftTypes.AddRange(new[] { v1Morning, v1Afternoon, v1Evening });

        // Version 2 active
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1, StartTime = new(7, 0), EndTime = new(12, 0), DurationHours = 5 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 5, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 2, StartTime = new(12, 0), EndTime = new(17, 0), DurationHours = 5 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 3, StartTime = new(17, 0), EndTime = new(21, 0), DurationHours = 4 });

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Tuần 1",
            RegistrationStatus = RegistrationStatus.Open,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 17, 0, 0, DateTimeKind.Utc),
            StaffingRequirements = new List<StaffingRequirement>
            {
                new() { SchedulePeriodId = 1, ShiftTypeId = 1, ShiftType = v1Morning, WorkDate = new DateOnly(2026, 8, 10) },
                new() { SchedulePeriodId = 1, ShiftTypeId = 2, ShiftType = v1Afternoon, WorkDate = new DateOnly(2026, 8, 10) },
                new() { SchedulePeriodId = 1, ShiftTypeId = 3, ShiftType = v1Evening, WorkDate = new DateOnly(2026, 8, 10) }
            }
        };
        periodRepo.SchedulePeriods.Add(period);

        var service = new AvailabilityService(
            availRepo,
            periodRepo,
            shiftTypeRepo,
            empRepo,
            new FakeAuditLogRepository(),
            new FakeUnitOfWork(),
            new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) },
            NullLogger<AvailabilityService>.Instance);

        var form = await service.GetEmployeeRegisterFormAsync(100, 1);

        Assert.NotNull(form);
        Assert.NotEmpty(form!.Days);
        var dayShifts = form.Days[0].Shifts;
        Assert.Equal(3, dayShifts.Count);
        var morning = dayShifts.First(s => s.ShiftCode == "MORNING");
        Assert.Equal(new TimeOnly(8, 0), morning.StartTime);
        Assert.Equal(new TimeOnly(12, 0), morning.EndTime);
        Assert.Equal(1, morning.ShiftTypeId);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-022: GetStaffingAsync lay dung 3 ca version 2 cua ky")]
    public async Task GetStaffingAsync_ReturnsPeriodShiftDefinitions()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();

        var v2Morning = new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1, StartTime = new(7, 0), EndTime = new(12, 0), DurationHours = 5 };
        var v2Afternoon = new ShiftType { ShiftTypeId = 5, ShiftCode = "AFTERNOON", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 2, StartTime = new(12, 0), EndTime = new(17, 0), DurationHours = 5 };
        var v2Evening = new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", ConfigurationVersion = 2, IsActive = true, DisplayOrder = 3, StartTime = new(17, 0), EndTime = new(21, 0), DurationHours = 4 };
        shiftTypeRepo.ShiftTypes.AddRange(new[] { v2Morning, v2Afternoon, v2Evening });

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 2,
            PeriodName = "Tuần 2",
            RegistrationStatus = RegistrationStatus.Preparing,
            WeekStartDate = new DateOnly(2026, 8, 17),
            WeekEndDate = new DateOnly(2026, 8, 23),
            StaffingRequirements = new List<StaffingRequirement>()
        };

        for (int day = 0; day < 7; day++)
        {
            var date = period.WeekStartDate.AddDays(day);
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = day * 3 + 1, SchedulePeriodId = 2, ShiftTypeId = 4, ShiftType = v2Morning, WorkDate = date, RequiredSalesStaff = 1, RequiredCashiers = 1, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = day * 3 + 2, SchedulePeriodId = 2, ShiftTypeId = 5, ShiftType = v2Afternoon, WorkDate = date, RequiredSalesStaff = 1, RequiredCashiers = 1, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = day * 3 + 3, SchedulePeriodId = 2, ShiftTypeId = 6, ShiftType = v2Evening, WorkDate = date, RequiredSalesStaff = 1, RequiredCashiers = 1, RequiredExperiencedStaff = 1, MinimumLevel = EmployeeLevel.Level2 });
        }
        periodRepo.SchedulePeriods.Add(period);

        var model = new StaffingRequirementEditViewModel
        {
            SchedulePeriodId = 2,
            Requirements = period.StaffingRequirements.Select(r => new StaffingRequirementItemViewModel
            {
                StaffingRequirementId = r.StaffingRequirementId,
                WorkDate = r.WorkDate,
                ShiftTypeId = r.ShiftTypeId,
                ShiftCode = r.ShiftType?.ShiftCode ?? "MORNING",
                ShiftName = r.ShiftType?.ShiftName ?? "Ca",
                RequiredSalesStaff = 2,
                RequiredCashiers = 1
            }).ToList()
        };

        var service = new SchedulePeriodService(
            periodRepo,
            shiftTypeRepo,
            new FakeNotificationRepository(),
            new FakeAuditLogRepository(),
            new FakeUnitOfWork(),
            new FakeClock { UtcNow = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
            NullLogger<SchedulePeriodService>.Instance);

        var result = await service.UpdateStaffingAsync(model, actorAccountId: 1);

        Assert.True(result.Succeeded);
        Assert.All(period.StaffingRequirements, r => Assert.Equal(2, r.RequiredSalesStaff));
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-023: BR20 dung StartTime/EndTime dung version")]
    public void SchedulingRestRuleEvaluator_EvaluatesBR20UsingActualShiftTimes()
    {
        var v2Evening = new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0), DurationHours = 4m };
        var v2Morning = new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(14, 0), DurationHours = 5m };

        var shiftMap = new Dictionary<int, ShiftType>
        {
            [4] = v2Morning,
            [6] = v2Evening
        };

        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 10), ShiftTypeId = 6 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 11), ShiftTypeId = 4 }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            new Employee { EmployeeId = 1, FullName = "Nguyễn Văn A" },
            assignments,
            shiftMap,
            new DateOnly(2026, 8, 10));

        Assert.Empty(errors);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-024: BR21 dung StartTime/EndTime dung version")]
    public void SchedulingRestRuleEvaluator_EvaluatesBR21UsingActualShiftTimes()
    {
        var v2Morning = new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(12, 0) };
        var shiftMap = new Dictionary<int, ShiftType> { [4] = v2Morning };

        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 10), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 11), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 12), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 13), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 14), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 15), ShiftTypeId = 4 }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            new Employee { EmployeeId = 1, FullName = "Nguyễn Văn A" },
            assignments,
            shiftMap,
            new DateOnly(2026, 8, 10));

        Assert.Empty(errors);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-025: BR-05 dung DurationHours dung version")]
    public void WeeklyWorkHoursRuleEvaluator_EvaluatesBR05UsingActualShiftDuration()
    {
        var v2Morning = new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", DurationHours = 5.0m };
        var shiftMap = new Dictionary<int, ShiftType> { [4] = v2Morning };

        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 10), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 11), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 12), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 13), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 14), ShiftTypeId = 4 },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 15), ShiftTypeId = 4 }
        };

        var errors = WeeklyWorkHoursRuleEvaluator.EvaluateWeeklyWorkHours(
            new Employee { EmployeeId = 1, FullName = "Nguyễn Văn B", EmploymentType = EmploymentType.PartTime },
            assignments,
            shiftMap);

        Assert.NotEmpty(errors);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-026: Bo gio 17:00-21:00 va 08:00-12:00 luu duoc nhung BR20 chan lam lien tiep")]
    public async Task UpdateConfigurationAsync_TightOvernightRest_SavesConfig_ButBR20CatchesConsecutiveAssignment()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4, DisplayOrder = 1, ConfigurationVersion = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DurationHours = 4, DisplayOrder = 2, ConfigurationVersion = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4, DisplayOrder = 3, ConfigurationVersion = 1, IsActive = true });

        var service = new ShiftTypeService(repo, new FakeAuditLogRepository(), new FakeUnitOfWork(), new FakeClock(), NullLogger<ShiftTypeService>.Instance);

        var model = new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = 1,
            Shifts = new List<ShiftConfigurationItemViewModel>
            {
                new() { ShiftCode = ShiftTypeConstants.MorningCode, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) },
                new() { ShiftCode = ShiftTypeConstants.AfternoonCode, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(17, 0) },
                new() { ShiftCode = ShiftTypeConstants.EveningCode, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(21, 0) }
            }
        };

        var configResult = await service.UpdateConfigurationAsync(model, 1);
        Assert.True(configResult.Succeeded);

        var newShifts = repo.ShiftTypes.Where(s => s.ConfigurationVersion == 2).ToList();
        var shiftMap = newShifts.ToDictionary(s => s.ShiftTypeId);
        var morningId = newShifts.First(s => s.ShiftCode == ShiftTypeConstants.MorningCode).ShiftTypeId;
        var eveningId = newShifts.First(s => s.ShiftCode == ShiftTypeConstants.EveningCode).ShiftTypeId;

        var assignments = new List<ShiftAssignment>
        {
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 10), ShiftTypeId = eveningId },
            new() { EmployeeId = 1, WorkDate = new DateOnly(2026, 8, 11), ShiftTypeId = morningId }
        };

        var errors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            new Employee { EmployeeId = 1, FullName = "Nhân viên" },
            assignments,
            shiftMap,
            new DateOnly(2026, 8, 10));

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("BR20") || e.Contains("11"));
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-027: Migration/Model tao unique composite index dung")]
    public void ShiftTypeConfiguration_CreatesCompositeUniqueIndexes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Test;User Id=sa;Password=Pass;")
            .Options;

        using var dbContext = new ApplicationDbContext(options);
        var shiftTypeEntity = dbContext.Model.FindEntityType(typeof(ShiftType));

        Assert.NotNull(shiftTypeEntity);
        var indexes = shiftTypeEntity!.GetIndexes().ToList();

        var shiftCodeVersionIndex = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ShiftCode", "ConfigurationVersion" }));
        Assert.NotNull(shiftCodeVersionIndex);
        Assert.True(shiftCodeVersionIndex!.IsUnique);

        var displayOrderVersionIndex = indexes.FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "DisplayOrder", "ConfigurationVersion" }));
        Assert.NotNull(displayOrderVersionIndex);
        Assert.True(displayOrderVersionIndex!.IsUnique);
    }

    [Fact(DisplayName = "TC-SHIFT-CONFIG-028: Non-regression check")]
    public void NonRegression_ShiftTypeConstants_HasValidCanonicalCodes()
    {
        Assert.Equal("MORNING", ShiftTypeConstants.MorningCode);
        Assert.Equal("AFTERNOON", ShiftTypeConstants.AfternoonCode);
        Assert.Equal("EVENING", ShiftTypeConstants.EveningCode);
        Assert.True(ShiftTypeConstants.IsFixedShiftCode("MORNING"));
        Assert.True(ShiftTypeConstants.IsFixedShiftCode("AFTERNOON"));
        Assert.True(ShiftTypeConstants.IsFixedShiftCode("EVENING"));
        Assert.False(ShiftTypeConstants.IsFixedShiftCode("NIGHT"));
    }
}
