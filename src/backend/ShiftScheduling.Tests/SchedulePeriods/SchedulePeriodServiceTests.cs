using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.SchedulePeriods;

public sealed class SchedulePeriodServiceTests
{
    [Fact(DisplayName = "TC03-B01 - UC03 BR03 - Có đúng 3 fixed shifts -> tạo đúng 21 StaffingRequirement")]
    public async Task CreateAsync_WhenValidFixedShifts_CreatesExactly21Requirements()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        var service = CreateService(periodRepo, shiftTypeRepo);
        var start = new DateOnly(2026, 8, 10);
        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ lịch tuần 33",
            WeekStartDate = start,
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc)
        };

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.True(result.Succeeded);
        Assert.Single(periodRepo.SchedulePeriods);
        Assert.Equal(21, periodRepo.Requirements.Count);
        Assert.All(periodRepo.Requirements, r => Assert.Equal(EmployeeLevel.Level2, r.MinimumLevel));
    }

    [Fact(DisplayName = "TC03-B02 - UC03 BR03 - Database có fixed 3 shifts + ca thứ 4 -> vẫn chỉ tạo 21 rows")]
    public async Task CreateAsync_WhenDatabaseHasFourthShift_IgnoresFourthShiftAndCreatesOnly21Rows()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        // Them ca thu 4 (NIGHT 20:00-24:00)
        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 4,
            ShiftCode = "NIGHT",
            ShiftName = "Ca đêm",
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(0, 0),
            DisplayOrder = 4,
            IsActive = true
        });

        var service = CreateService(periodRepo, shiftTypeRepo);
        var start = new DateOnly(2026, 8, 10);
        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ lịch tuần 33",
            WeekStartDate = start,
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc)
        };

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.True(result.Succeeded);
        Assert.Equal(21, periodRepo.Requirements.Count);
        Assert.DoesNotContain(periodRepo.Requirements, r => r.ShiftTypeId == 4);
    }

    [Fact(DisplayName = "TC03-B03 - UC03 BR03 - Thiếu MORNING / AFTERNOON / EVENING -> từ chối tạo period")]
    public async Task CreateAsync_WhenMissingFixedShift_ReturnsShiftTypeError()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository(); // Emtpy
        var service = CreateService(periodRepo, shiftTypeRepo);
        var model = new SchedulePeriodCreateViewModel
        {
            WeekStartDate = new DateOnly(2026, 8, 10),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc)
        };

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTypeNotFound, result.ErrorCode);
    }

    [Fact(DisplayName = "TC03-B04 - UC03 BR03 - Một fixed shift có sai StartTime/EndTime -> từ chối tạo period")]
    public async Task CreateAsync_WhenFixedShiftHasInvalidTime_ReturnsShiftTypeError()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(11, 0), IsActive = true });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), IsActive = true });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), IsActive = true });

        var service = CreateService(periodRepo, shiftTypeRepo);
        var model = new SchedulePeriodCreateViewModel
        {
            WeekStartDate = new DateOnly(2026, 8, 10),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc)
        };

        // Thuc hien
        var result = await service.CreateAsync(model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTypeNotFound, result.ErrorCode);
    }

    [Fact(DisplayName = "TC03-B05 - UC03 BR03 - UpdateStaffing với ShiftType thứ 4 tồn tại thật -> bị từ chối")]
    public async Task UpdateStaffingAsync_WhenUsingFourthShift_ReturnsShiftTypeError()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 4, ShiftCode = "NIGHT", StartTime = new TimeOnly(20, 0), EndTime = new TimeOnly(0, 0), IsActive = true });

        var service = CreateService(periodRepo, shiftTypeRepo);
        var period = CreateValidPeriodWith21Requirements();
        periodRepo.Add(period);

        var model = new StaffingRequirementEditViewModel
        {
            SchedulePeriodId = 1,
            Requirements = new List<StaffingRequirementItemViewModel>
            {
                new()
                {
                    WorkDate = period.WeekStartDate,
                    ShiftTypeId = 4, // Ca thứ 4
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1
                }
            }
        };

        // Thuc hien
        var result = await service.UpdateStaffingAsync(model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTypeNotFound, result.ErrorCode);
    }

    [Fact(DisplayName = "TC03-B06 - UC03 BR03 - OpenRegistration với >21 / sai combination -> bị từ chối")]
    public async Task OpenRegistrationAsync_WhenMoreThan21Requirements_ReturnsRequirementError()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        var service = CreateService(periodRepo, shiftTypeRepo);
        var period = CreateValidPeriodWith21Requirements();
        // Them row thu 22 (NIGHT)
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            StaffingRequirementId = 22,
            SchedulePeriodId = 1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 4,
            RequiredSalesStaff = 1,
            RequiredCashiers = 1,
            RequiredExperiencedStaff = 1,
            MinimumLevel = EmployeeLevel.Level2
        });
        periodRepo.Add(period);

        // Thuc hien
        var result = await service.OpenRegistrationAsync(1, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidStaffingRequirement, result.ErrorCode);
        Assert.Equal(RegistrationStatus.Preparing, period.RegistrationStatus);
    }

    [Fact(DisplayName = "TC03-C01 - UC03 BR12 - Một requirement Sales < 0 -> không OpenRegistration")]
    public async Task OpenRegistrationAsync_WhenSalesNegative_ReturnsBR12Error()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var service = CreateService(periodRepo, CreateShiftTypeRepositoryWithThreeFixedShifts());
        var period = CreateValidPeriodWith21Requirements();
        period.StaffingRequirements.First().RequiredSalesStaff = -1;
        periodRepo.Add(period);

        // Thuc hien
        var result = await service.OpenRegistrationAsync(1, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidStaffingRequirement, result.ErrorCode);
        Assert.Equal(RegistrationStatus.Preparing, period.RegistrationStatus);
    }

    [Fact(DisplayName = "TC03-C02 - UC03 BR12 - Một requirement Cashier < 0 -> không OpenRegistration")]
    public async Task OpenRegistrationAsync_WhenCashierNegative_ReturnsBR12Error()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var service = CreateService(periodRepo, CreateShiftTypeRepositoryWithThreeFixedShifts());
        var period = CreateValidPeriodWith21Requirements();
        period.StaffingRequirements.First().RequiredCashiers = -1;
        periodRepo.Add(period);

        // Thuc hien
        var result = await service.OpenRegistrationAsync(1, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidStaffingRequirement, result.ErrorCode);
        Assert.Equal(RegistrationStatus.Preparing, period.RegistrationStatus);
    }

    [Fact(DisplayName = "TC03-C03 - UC03 BR12 - Một requirement Sales = 0 và Cashier = 0 -> không OpenRegistration")]
    public async Task OpenRegistrationAsync_WhenTotalZero_ReturnsBR12Error()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var service = CreateService(periodRepo, CreateShiftTypeRepositoryWithThreeFixedShifts());
        var period = CreateValidPeriodWith21Requirements();
        period.StaffingRequirements.First().RequiredSalesStaff = 0;
        period.StaffingRequirements.First().RequiredCashiers = 0;
        periodRepo.Add(period);

        // Thuc hien
        var result = await service.OpenRegistrationAsync(1, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidStaffingRequirement, result.ErrorCode);
        Assert.Equal(RegistrationStatus.Preparing, period.RegistrationStatus);
    }

    [Fact(DisplayName = "TC03-D01 - UC03 BR13 - RequiredExperiencedStaff = 0 -> không OpenRegistration")]
    public async Task OpenRegistrationAsync_WhenExperiencedStaffZero_ReturnsBR13Error()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var service = CreateService(periodRepo, CreateShiftTypeRepositoryWithThreeFixedShifts());
        var period = CreateValidPeriodWith21Requirements();
        period.StaffingRequirements.First().RequiredExperiencedStaff = 0;
        periodRepo.Add(period);

        // Thuc hien
        var result = await service.OpenRegistrationAsync(1, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidStaffingRequirement, result.ErrorCode);
        Assert.Equal(RegistrationStatus.Preparing, period.RegistrationStatus);
    }

    [Fact(DisplayName = "TC03-D02 - UC03 BR13 - MinimumLevel = Level1 -> không OpenRegistration")]
    public async Task OpenRegistrationAsync_WhenMinimumLevelIsLevel1_ReturnsBR13Error()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var service = CreateService(periodRepo, CreateShiftTypeRepositoryWithThreeFixedShifts());
        var period = CreateValidPeriodWith21Requirements();
        period.StaffingRequirements.First().MinimumLevel = EmployeeLevel.Level1;
        periodRepo.Add(period);

        // Thuc hien
        var result = await service.OpenRegistrationAsync(1, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidStaffingRequirement, result.ErrorCode);
        Assert.Equal(RegistrationStatus.Preparing, period.RegistrationStatus);
    }

    private static SchedulePeriodService CreateService(
        FakeSchedulePeriodRepository periodRepo,
        FakeShiftTypeRepository shiftTypeRepo)
    {
        return new SchedulePeriodService(
            periodRepo,
            shiftTypeRepo,
            new FakeAuditLogRepository(),
            new FakeUnitOfWork(),
            new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) },
            NullLogger<SchedulePeriodService>.Instance);
    }

    private static FakeShiftTypeRepository CreateShiftTypeRepositoryWithThreeFixedShifts()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DisplayOrder = 1,
            IsActive = true
        });
        repo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca chiều",
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(16, 0),
            DisplayOrder = 2,
            IsActive = true
        });
        repo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 3,
            ShiftCode = "EVENING",
            ShiftName = "Ca tối",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(20, 0),
            DisplayOrder = 3,
            IsActive = true
        });
        return repo;
    }

    [Fact(DisplayName = "TC03-08 - UC03 BR03 - Duplicate SchedulePeriod for same week is rejected")]
    public async Task CreateAsync_WhenWeekAlreadyExists_ReturnsOverlapError()
    {
        // Sap xep
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        var service = CreateService(periodRepo, shiftTypeRepo);
        var start = new DateOnly(2026, 8, 10);
        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ lịch tuần 33",
            WeekStartDate = start,
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc)
        };

        // Tao lan 1
        var result1 = await service.CreateAsync(model, 1);
        Assert.True(result1.Succeeded);

        // Tao lan 2 cung weekStartDate
        var result2 = await service.CreateAsync(model, 1);
        Assert.False(result2.Succeeded);
        Assert.Equal(ErrorCodes.SchedulePeriodOverlap, result2.ErrorCode);
    }

    private static SchedulePeriod CreateValidPeriodWith21Requirements()
    {
        var start = new DateOnly(2026, 8, 10);
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ lịch 10/08 - 16/08",
            WeekStartDate = start,
            WeekEndDate = start.AddDays(6),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Preparing,
            CreatedByAccountId = 1,
            StaffingRequirements = new List<StaffingRequirement>()
        };

        var shiftTypes = new[]
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DisplayOrder = 1, IsActive = true },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DisplayOrder = 2, IsActive = true },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DisplayOrder = 3, IsActive = true }
        };

        int idCounter = 1;
        for (int day = 0; day < 7; day++)
        {
            var date = start.AddDays(day);
            foreach (var shift in shiftTypes)
            {
                period.StaffingRequirements.Add(new StaffingRequirement
                {
                    StaffingRequirementId = idCounter++,
                    SchedulePeriodId = 1,
                    SchedulePeriod = period,
                    WorkDate = date,
                    ShiftTypeId = shift.ShiftTypeId,
                    ShiftType = shift,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }

        return period;
    }
}
