using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.SchedulePeriods;

public sealed class SchedulePeriodRegistrationTests
{
    [Fact(DisplayName = "TC-LC-006: Deadline trước ngày bắt đầu -> Hợp lệ")]
    public async Task CreateAsync_DeadlineBeforeStartDate_Succeeds()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftRepo = new FakeShiftTypeRepository();
        shiftRepo.ShiftTypes.AddRange(new[]
        {
            new ShiftType
            {
                ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", IsActive = true,
                ConfigurationVersion = 1, DisplayOrder = 1,
                StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0)
            },
            new ShiftType
            {
                ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", IsActive = true,
                ConfigurationVersion = 1, DisplayOrder = 2,
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0)
            },
            new ShiftType
            {
                ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", IsActive = true,
                ConfigurationVersion = 1, DisplayOrder = 3,
                StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0)
            }
        });

        var clock = new FakeClock(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, shiftRepo, new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ tháng 9",
            WeekStartDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 9, 5, 23, 59, 0)
        };

        var result = await service.CreateAsync(model, 1);
        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "TC-LC-007: Deadline cùng hoặc sau ngày bắt đầu -> Từ chối")]
    public async Task CreateAsync_DeadlineOnOrAfterStartDate_ReturnsFailure()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftRepo = new FakeShiftTypeRepository();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, shiftRepo, new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ vi phạm",
            WeekStartDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 9, 7, 10, 0, 0)
        };

        var result = await service.CreateAsync(model, 1);
        Assert.False(result.Succeeded);
        Assert.Equal(
            "Hạn đăng ký lịch rảnh phải trước ngày bắt đầu kỳ lịch.",
            result.ErrorMessage);
    }

    [Fact(DisplayName =
        "TC-LC-008: Mở đăng ký khi đã tới ngày bắt đầu -> Từ chối với InvalidSchedulePeriodStatus")]
    public async Task OpenRegistrationAsync_PeriodAlreadyStarted_ReturnsFailure()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 8, 30, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Preparing
        });

        var clock = new FakeClock(new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePeriodStatus, result.ErrorCode);
        Assert.Equal("Kỳ lịch đã bắt đầu, không thể mở đăng ký.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-009: POST Availability bị chặn khi kỳ đã bắt đầu dù DB vẫn lưu Open")]
    public async Task SaveEmployeeAvailabilityAsync_PeriodStartedButStoredOpen_ReturnsFailure()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 9, 5, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var empRepo = new FakeEmployeeRepository();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        var clock = new FakeClock(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
        var service = new AvailabilityService(
            new FakeAvailabilityRepository(), periodRepo, new FakeShiftTypeRepository(), empRepo,
            new FakeAuditLogRepository(), new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(), clock, NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(
            100, 1, new List<string> { "2026-09-02_10" });

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePeriodStatus, result.ErrorCode);
        Assert.Equal(
            "Kỳ lịch đã bắt đầu, không thể đăng ký thêm lịch rảnh.",
            result.ErrorMessage);
    }

    [Fact(DisplayName =
        "TC-LC-013: UtcNow == AvailabilityDeadline, kỳ Open và chưa bắt đầu -> Vẫn cho đăng ký")]
    public async Task SaveEmployeeAvailabilityAsync_ExactlyAtDeadline_Succeeds()
    {
        var deadline = new DateTime(2026, 9, 5, 23, 59, 0, DateTimeKind.Utc);
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = deadline,
            RegistrationStatus = RegistrationStatus.Open
        });

        var shiftRepo = new FakeShiftTypeRepository();
        shiftRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 10, ShiftCode = "MORNING", IsActive = true });

        var empRepo = new FakeEmployeeRepository();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        var clock = new FakeClock(deadline);
        var service = new AvailabilityService(
            new FakeAvailabilityRepository(), periodRepo, shiftRepo, empRepo,
            new FakeAuditLogRepository(), new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(), clock, NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(
            100, 1, new List<string> { "2026-09-07_10" });

        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName =
        "TC-LC-014: UtcNow sau deadline 1s, kỳ chưa bắt đầu -> Từ chối RegistrationDeadlineExpired")]
    public async Task SaveEmployeeAvailabilityAsync_JustAfterDeadline_ReturnsDeadlineExpired()
    {
        var deadline = new DateTime(2026, 9, 5, 23, 59, 0, DateTimeKind.Utc);
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = deadline,
            RegistrationStatus = RegistrationStatus.Open
        });

        var empRepo = new FakeEmployeeRepository();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        var clock = new FakeClock(deadline.AddSeconds(1));
        var service = new AvailabilityService(
            new FakeAvailabilityRepository(), periodRepo, new FakeShiftTypeRepository(), empRepo,
            new FakeAuditLogRepository(), new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(), clock, NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(
            100, 1, new List<string> { "2026-09-07_10" });

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.RegistrationDeadlineExpired, result.ErrorCode);
        Assert.Equal("Đã hết thời hạn đăng ký lịch rảnh.", result.ErrorMessage);
    }

    [Fact(DisplayName =
        "TC-LC-015: OpenRegistration - Deadline trùng ngày bắt đầu kỳ lịch -> Từ chối InvalidDateRange")]
    public async Task OpenRegistrationAsync_DeadlineOnStartDate_ReturnsInvalidDateRange()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Preparing
        });

        var clock = new FakeClock(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidDateRange, result.ErrorCode);
        Assert.Equal(
            "Hạn đăng ký lịch rảnh phải trước ngày bắt đầu kỳ lịch.",
            result.ErrorMessage);
    }

    [Fact(DisplayName =
        "TC-LC-016: OpenRegistration - Deadline sau ngày bắt đầu kỳ lịch -> Từ chối InvalidDateRange")]
    public async Task OpenRegistrationAsync_DeadlineAfterStartDate_ReturnsInvalidDateRange()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Preparing
        });

        var clock = new FakeClock(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidDateRange, result.ErrorCode);
        Assert.Equal(
            "Hạn đăng ký lịch rảnh phải trước ngày bắt đầu kỳ lịch.",
            result.ErrorMessage);
    }

    [Fact(DisplayName =
        "TC-LC-017: OpenRegistration - Deadline đã qua -> Từ chối RegistrationDeadlineExpired")]
    public async Task OpenRegistrationAsync_DeadlinePassedBeforeStartDate_ReturnsDeadlineExpired()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = new DateTime(2026, 9, 3, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Preparing
        });

        var clock = new FakeClock(new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.RegistrationDeadlineExpired, result.ErrorCode);
        Assert.Equal(
            "Thời hạn đăng ký lịch rảnh đã hết. Không thể mở đăng ký.",
            result.ErrorMessage);
    }
}
