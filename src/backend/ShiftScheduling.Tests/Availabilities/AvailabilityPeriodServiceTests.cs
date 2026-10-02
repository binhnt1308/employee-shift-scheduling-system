using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Availabilities;

public sealed class AvailabilityPeriodServiceTests
{
    [Fact(DisplayName = "UC04 - Danh sách kỳ đăng ký dùng batch query để đếm lịch rảnh")]
    public async Task GetEmployeePeriodsAsync_WithMultiplePeriods_UsesSingleBatchCountQuery()
    {
        var availabilityRepository = new FakeAvailabilityRepository();
        var periodRepository = new FakeSchedulePeriodRepository();
        var employeeRepository = new FakeEmployeeRepository();

        SeedEmployeeAndPeriods(employeeRepository, periodRepository);
        SeedAvailabilities(availabilityRepository);

        var service = CreateService(
            availabilityRepository,
            periodRepository,
            employeeRepository);

        var result = await service.GetEmployeePeriodsAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Single(period => period.SchedulePeriodId == 1).RegisteredCount);
        Assert.Equal(1, result.Single(period => period.SchedulePeriodId == 2).RegisteredCount);
        Assert.Equal(1, availabilityRepository.BatchCountQueryCallCount);
        Assert.Equal(0, availabilityRepository.EmployeeAndPeriodQueryCallCount);
    }

    private static void SeedEmployeeAndPeriods(
        FakeEmployeeRepository employeeRepository,
        FakeSchedulePeriodRepository periodRepository)
    {
        employeeRepository.Add(new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV001",
            FullName = "Nguyễn Văn A",
            UserAccountId = 10,
            IsActive = true
        });

        periodRepository.Add(CreatePeriod(
            1,
            "Kỳ 1",
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 16),
            new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc)));

        periodRepository.Add(CreatePeriod(
            2,
            "Kỳ 2",
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 8, 23),
            new DateTime(2026, 8, 15, 23, 59, 0, DateTimeKind.Utc)));
    }

    private static void SeedAvailabilities(
        FakeAvailabilityRepository availabilityRepository)
    {
        availabilityRepository.Availabilities.AddRange(new[]
        {
            CreateAvailability(1, 1, new DateOnly(2026, 8, 10), 1),
            CreateAvailability(1, 1, new DateOnly(2026, 8, 11), 2),
            CreateAvailability(1, 2, new DateOnly(2026, 8, 17), 1),
            CreateAvailability(999, 1, new DateOnly(2026, 8, 10), 3)
        });
    }

    private static AvailabilityService CreateService(
        FakeAvailabilityRepository availabilityRepository,
        FakeSchedulePeriodRepository periodRepository,
        FakeEmployeeRepository employeeRepository)
    {
        return new AvailabilityService(
            availabilityRepository,
            periodRepository,
            new FakeShiftTypeRepository(),
            employeeRepository,
            new FakeAuditLogRepository(),
            new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(),
            new FakeClock(new DateTime(
                2026, 8, 5, 10, 0, 0, DateTimeKind.Utc)),
            NullLogger<AvailabilityService>.Instance);
    }

    private static SchedulePeriod CreatePeriod(
        int id,
        string name,
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        DateTime deadline)
    {
        return new SchedulePeriod
        {
            SchedulePeriodId = id,
            PeriodName = name,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            AvailabilityDeadline = deadline,
            RegistrationStatus = RegistrationStatus.Open
        };
    }

    private static Availability CreateAvailability(
        int employeeId,
        int periodId,
        DateOnly workDate,
        int shiftTypeId)
    {
        return new Availability
        {
            EmployeeId = employeeId,
            SchedulePeriodId = periodId,
            WorkDate = workDate,
            ShiftTypeId = shiftTypeId
        };
    }
}
