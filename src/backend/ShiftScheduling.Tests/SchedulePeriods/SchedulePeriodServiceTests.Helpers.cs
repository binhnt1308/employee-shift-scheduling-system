using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.SchedulePeriods;

public sealed partial class SchedulePeriodServiceTests
{
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
        repo.ShiftTypes.AddRange(CreateThreeFixedShiftTypes());
        return repo;
    }

    private static ShiftType[] CreateThreeFixedShiftTypes() =>
    [
        new()
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng",
            StartTime = new(8, 0),
            EndTime = new(12, 0),
            DisplayOrder = 1,
            IsActive = true
        },
        new()
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca chiều",
            StartTime = new(12, 0),
            EndTime = new(16, 0),
            DisplayOrder = 2,
            IsActive = true
        },
        new()
        {
            ShiftTypeId = 3,
            ShiftCode = "EVENING",
            ShiftName = "Ca tối",
            StartTime = new(16, 0),
            EndTime = new(20, 0),
            DisplayOrder = 3,
            IsActive = true
        }
    ];

    [Fact(DisplayName = "TC03-08 - UC03 BR03 - Duplicate SchedulePeriod for same week is rejected")]
    public async Task CreateAsync_WhenWeekAlreadyExists_ReturnsOverlapError()
    {
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

        var result1 = await service.CreateAsync(model, 1);
        Assert.True(result1.Succeeded);

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

        var shiftTypes = CreateThreeFixedShiftTypes();
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
