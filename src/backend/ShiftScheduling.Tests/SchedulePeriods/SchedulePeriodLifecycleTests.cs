using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.Dashboard.Services;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.SchedulePeriods;

public sealed class SchedulePeriodLifecycleTests
{
    [Fact(DisplayName = "TC-LC-01: Có lịch chưa công bố và kỳ đã bắt đầu -> Status 'Chưa công bố' và Cần xử lý")]
    public void CalculateEffectiveStatus_DraftScheduleAndPeriodStarted_ReturnsUnpublishedWithWarning()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ 1",
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 8, 30, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };

        var workSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Draft
        };

        var currentUtc = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 2);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, workSchedule, currentUtc, currentDate);

        Assert.Equal("Chưa công bố", result.StatusName);
        Assert.True(result.NeedsAttention);
        Assert.Equal("Kỳ lịch đã bắt đầu nhưng lịch làm việc chưa được công bố.", result.AttentionMessage);
    }

    [Fact(DisplayName = "TC-LC-02: Không có lịch và kỳ đã bắt đầu -> Status 'Quá hạn – chưa phân công' và Cần xử lý")]
    public void CalculateEffectiveStatus_NoScheduleAndPeriodStarted_ReturnsOverdueUnassignedWithWarning()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ 1",
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 8, 30, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };

        var currentUtc = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 2);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, null, currentUtc, currentDate);

        Assert.Equal("Quá hạn – chưa phân công", result.StatusName);
        Assert.True(result.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-03: Có lịch đã công bố và kỳ đang diễn ra -> Status 'Đã công bố'")]
    public void CalculateEffectiveStatus_PublishedScheduleAndOngoing_ReturnsPublished()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7)
        };

        var workSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            Status = ScheduleStatus.Published
        };

        var currentUtc = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 3);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, workSchedule, currentUtc, currentDate);

        Assert.Equal("Đã công bố", result.StatusName);
        Assert.False(result.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-04: Kỳ kết thúc có lịch công bố -> Status 'Đã kết thúc'")]
    public void CalculateEffectiveStatus_EndedPeriodWithPublishedSchedule_ReturnsEnded()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 1),
            WeekEndDate = new DateOnly(2026, 8, 7)
        };

        var workSchedule = new WorkSchedule { WorkScheduleId = 10, Status = ScheduleStatus.Published };

        var currentUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 1);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, workSchedule, currentUtc, currentDate);

        Assert.Equal("Đã kết thúc", result.StatusName);
    }

    [Fact(DisplayName = "TC-LC-05: Kỳ kết thúc không có lịch -> Status 'Đã kết thúc – chưa phân công'")]
    public void CalculateEffectiveStatus_EndedPeriodNoSchedule_ReturnsEndedUnassigned()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 1),
            WeekEndDate = new DateOnly(2026, 8, 7)
        };

        var currentUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 1);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, null, currentUtc, currentDate);

        Assert.Equal("Đã kết thúc – chưa phân công", result.StatusName);
    }

    [Fact(DisplayName = "TC-LC-06: Deadline trước ngày bắt đầu -> Hợp lệ")]
    public async Task CreateAsync_DeadlineBeforeStartDate_Succeeds()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftRepo = new FakeShiftTypeRepository();
        shiftRepo.ShiftTypes.AddRange(new[]
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", IsActive = true, ConfigurationVersion = 1, DisplayOrder = 1, StartTime = new TimeOnly(8,0), EndTime = new TimeOnly(12,0) },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", IsActive = true, ConfigurationVersion = 1, DisplayOrder = 2, StartTime = new TimeOnly(12,0), EndTime = new TimeOnly(16,0) },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", IsActive = true, ConfigurationVersion = 1, DisplayOrder = 3, StartTime = new TimeOnly(16,0), EndTime = new TimeOnly(20,0) }
        });

        var clock = new FakeClock(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, shiftRepo, new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ tháng 9",
            WeekStartDate = new DateOnly(2026, 9, 7), // Thứ 2
            AvailabilityDeadline = new DateTime(2026, 9, 5, 23, 59, 0) // Thứ 7 trước
        };

        var result = await service.CreateAsync(model, 1);
        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "TC-LC-07: Deadline cùng hoặc sau ngày bắt đầu -> Từ chối")]
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
            AvailabilityDeadline = new DateTime(2026, 9, 7, 10, 0, 0) // Cùng ngày bắt đầu
        };

        var result = await service.CreateAsync(model, 1);
        Assert.False(result.Succeeded);
        Assert.Equal("Hạn đăng ký lịch rảnh phải trước ngày bắt đầu kỳ lịch.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-08: Mở đăng ký khi đã tới ngày bắt đầu -> Từ chối với InvalidSchedulePeriodStatus")]
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

        // Thời gian hiện tại đã là ngày 2026-09-01 (Ngày bắt đầu)
        var clock = new FakeClock(new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePeriodStatus, result.ErrorCode);
        Assert.Equal("Kỳ lịch đã bắt đầu, không thể mở đăng ký.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-09: POST Availability bị chặn khi kỳ đã bắt đầu dù DB vẫn lưu Open")]
    public async Task SaveEmployeeAvailabilityAsync_PeriodStartedButStoredOpen_ReturnsFailure()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 9, 5, 23, 59, 0, DateTimeKind.Utc), // Deadline tương lai
            RegistrationStatus = RegistrationStatus.Open // DB vẫn lưu Open
        });

        var empRepo = new FakeEmployeeRepository();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        // Thời gian hiện tại là 2026-09-02 (Kỳ đã bắt đầu)
        var clock = new FakeClock(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));

        var service = new AvailabilityService(
            new FakeAvailabilityRepository(), periodRepo, new FakeShiftTypeRepository(), empRepo,
            new FakeAuditLogRepository(), new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(), clock, NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, new List<string> { "2026-09-02_10" });

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePeriodStatus, result.ErrorCode);
        Assert.Equal("Kỳ lịch đã bắt đầu, không thể đăng ký thêm lịch rảnh.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-10: Dashboard chọn kỳ đang diễn ra thay vì lịch công bố cũ của kỳ khác")]
    public async Task GetDashboardAsync_OngoingPeriodExists_SelectsOngoingPeriodOverOldPublishedSchedule()
    {
        var periodRepo = new FakeSchedulePeriodRepository();

        // Kỳ cũ có lịch công bố
        var oldPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ cũ",
            WeekStartDate = new DateOnly(2026, 8, 1),
            WeekEndDate = new DateOnly(2026, 8, 7)
        };
        oldPeriod.WorkSchedules.Add(new WorkSchedule { WorkScheduleId = 100, SchedulePeriodId = 1, Status = ScheduleStatus.Published });
        periodRepo.Add(oldPeriod);

        // Kỳ hiện tại đang diễn ra nhưng CHƯA CÓ LỊCH
        var ongoingPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 2,
            PeriodName = "Kỳ hiện tại",
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7)
        };
        periodRepo.Add(ongoingPeriod);

        // Current time: 2026-09-03
        var clock = new FakeClock(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));

        var service = new DashboardService(
            new FakeAccountRepository(), new FakeEmployeeRepository(), new FakeShiftTypeRepository(),
            periodRepo, clock);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal("Kỳ hiện tại", dashboard.TargetPeriodName);
        Assert.Equal("Quá hạn – chưa phân công", dashboard.ScheduleStatusDisplay);
        Assert.True(dashboard.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-11: Không có kỳ hiện tại -> Chọn kỳ sắp tới gần nhất")]
    public async Task GetDashboardAsync_NoOngoingPeriod_SelectsNearestUpcomingPeriod()
    {
        var periodRepo = new FakeSchedulePeriodRepository();

        var upcomingPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 2,
            PeriodName = "Kỳ tuần sau",
            WeekStartDate = new DateOnly(2026, 9, 14),
            WeekEndDate = new DateOnly(2026, 9, 20),
            AvailabilityDeadline = new DateTime(2026, 9, 12, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        periodRepo.Add(upcomingPeriod);

        // Current time: 2026-09-03
        var clock = new FakeClock(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));

        var service = new DashboardService(
            new FakeAccountRepository(), new FakeEmployeeRepository(), new FakeShiftTypeRepository(),
            periodRepo, clock);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal("Kỳ tuần sau", dashboard.TargetPeriodName);
        Assert.Equal("Đang mở đăng ký", dashboard.ScheduleStatusDisplay);
        Assert.False(dashboard.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-12: Target period có lịch công bố -> Dashboard hiện 'Đã công bố'")]
    public async Task GetDashboardAsync_TargetPeriodPublished_ReturnsPublishedStatus()
    {
        var periodRepo = new FakeSchedulePeriodRepository();

        var ongoingPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 2,
            PeriodName = "Kỳ 2",
            WeekStartDate = new DateOnly(2026, 9, 1),
            WeekEndDate = new DateOnly(2026, 9, 7)
        };
        ongoingPeriod.WorkSchedules.Add(new WorkSchedule { WorkScheduleId = 200, SchedulePeriodId = 2, Status = ScheduleStatus.Published });
        periodRepo.Add(ongoingPeriod);

        var clock = new FakeClock(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));

        var service = new DashboardService(
            new FakeAccountRepository(), new FakeEmployeeRepository(), new FakeShiftTypeRepository(),
            periodRepo, clock);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal("Kỳ 2", dashboard.TargetPeriodName);
        Assert.Equal("Đã công bố", dashboard.ScheduleStatusDisplay);
        Assert.True(dashboard.HasSchedule);
    }

    [Fact(DisplayName = "TC-LC-13: UtcNow == AvailabilityDeadline, kỳ Open và chưa bắt đầu -> Vẫn cho đăng ký")]
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

        // UtcNow đúng bằng deadline
        var clock = new FakeClock(deadline);

        var service = new AvailabilityService(
            new FakeAvailabilityRepository(), periodRepo, shiftRepo, empRepo,
            new FakeAuditLogRepository(), new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(), clock, NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, new List<string> { "2026-09-07_10" });

        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "TC-LC-14: UtcNow sau deadline 1s, kỳ chưa bắt đầu -> Từ chối RegistrationDeadlineExpired")]
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

        // UtcNow trễ 1 giây so với deadline
        var clock = new FakeClock(deadline.AddSeconds(1));

        var service = new AvailabilityService(
            new FakeAvailabilityRepository(), periodRepo, new FakeShiftTypeRepository(), empRepo,
            new FakeAuditLogRepository(), new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(), clock, NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, new List<string> { "2026-09-07_10" });

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.RegistrationDeadlineExpired, result.ErrorCode);
        Assert.Equal("Đã hết thời hạn đăng ký lịch rảnh.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-15: OpenRegistration - Deadline trùng ngày bắt đầu kỳ lịch -> Từ chối InvalidDateRange")]
    public async Task OpenRegistrationAsync_DeadlineOnStartDate_ReturnsInvalidDateRange()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), // Trùng ngày bắt đầu
            RegistrationStatus = RegistrationStatus.Preparing
        });

        var clock = new FakeClock(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidDateRange, result.ErrorCode);
        Assert.Equal("Hạn đăng ký lịch rảnh phải trước ngày bắt đầu kỳ lịch.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-16: OpenRegistration - Deadline sau ngày bắt đầu kỳ lịch -> Từ chối InvalidDateRange")]
    public async Task OpenRegistrationAsync_DeadlineAfterStartDate_ReturnsInvalidDateRange()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc), // Sau ngày bắt đầu
            RegistrationStatus = RegistrationStatus.Preparing
        });

        var clock = new FakeClock(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidDateRange, result.ErrorCode);
        Assert.Equal("Hạn đăng ký lịch rảnh phải trước ngày bắt đầu kỳ lịch.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-17: OpenRegistration - Deadline đã qua nhưng chưa đến ngày bắt đầu -> Từ chối RegistrationDeadlineExpired")]
    public async Task OpenRegistrationAsync_DeadlinePassedBeforeStartDate_ReturnsDeadlineExpired()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 7),
            WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = new DateTime(2026, 9, 3, 23, 59, 0, DateTimeKind.Utc), // Deadline hợp lệ < StartDate
            RegistrationStatus = RegistrationStatus.Preparing
        });

        // Thời gian hiện tại là 2026-09-04 (đã qua deadline nhưng chưa đến StartDate)
        var clock = new FakeClock(new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo, new FakeShiftTypeRepository(), new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(1, 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.RegistrationDeadlineExpired, result.ErrorCode);
        Assert.Equal("Thời hạn đăng ký lịch rảnh đã hết. Không thể mở đăng ký.", result.ErrorMessage);
    }

    [Fact(DisplayName = "TC-LC-18: Trạng thái kỳ kết thúc trả DisplayBadgeClass badge-ended rõ ràng")]
    public void CalculateEffectiveStatus_EndedPeriod_ReturnsBadgeEndedClass()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ đã qua",
            WeekStartDate = new DateOnly(2026, 8, 3),
            WeekEndDate = new DateOnly(2026, 8, 9)
        };

        var currentUtc = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 8, 15);

        // Case A: Đã kết thúc có WorkSchedule Published
        var publishedSchedule = new WorkSchedule { WorkScheduleId = 10, Status = ScheduleStatus.Published };
        var statusA = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, publishedSchedule, currentUtc, currentDate);
        Assert.Equal("Đã kết thúc", statusA.StatusName);
        Assert.Equal("badge-ended", statusA.DisplayBadgeClass);

        // Case B: Đã kết thúc – chưa phân công (không có WorkSchedule)
        var statusB = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, null, currentUtc, currentDate);
        Assert.Equal("Đã kết thúc – chưa phân công", statusB.StatusName);
        Assert.Equal("badge-ended", statusB.DisplayBadgeClass);
    }

    [Fact(DisplayName = "TC-LC-19: Kỳ mới dùng cấu hình đang hoạt động, cấu hình mới sau đó không ảnh hưởng kỳ cũ")]
    public async Task CreateAsync_UsesActiveShifts_AndFutureShiftChangesDoNotAffectExistingPeriod()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));

        // Version 1 active shifts (Ids 1, 2, 3)
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new(8, 0), EndTime = new(12, 0), ConfigurationVersion = 1, IsActive = true, DisplayOrder = 1 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new(12, 0), EndTime = new(16, 0), ConfigurationVersion = 1, IsActive = true, DisplayOrder = 2 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new(16, 0), EndTime = new(20, 0), ConfigurationVersion = 1, IsActive = true, DisplayOrder = 3 });

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, new FakeNotificationRepository(),
            new FakeAuditLogRepository(), unitOfWork, clock, NullLogger<SchedulePeriodService>.Instance);

        // Create Period 1
        var model1 = new ShiftScheduling.Features.SchedulePeriods.ViewModels.SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ 1",
            WeekStartDate = new DateOnly(2026, 9, 14),
            AvailabilityDeadline = new DateTime(2026, 9, 10, 12, 0, 0)
        };

        var result1 = await service.CreateAsync(model1, actorAccountId: 1);
        Assert.True(result1.Succeeded);

        // Period 1 requirements should reference version 1 shift IDs (1, 2, 3)
        var period1 = periodRepo.SchedulePeriods[0];
        Assert.Equal(21, period1.StaffingRequirements.Count);
        Assert.All(period1.StaffingRequirements, req => Assert.Contains(req.ShiftTypeId, new[] { 1, 2, 3 }));

        // Now, update shift configuration to Version 2 (Ids 4, 5, 6 with changed times)
        foreach (var s in shiftTypeRepo.ShiftTypes)
        {
            s.IsActive = false;
        }
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ShiftName = "Ca sáng mới", StartTime = new(7, 30), EndTime = new(11, 30), ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 5, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều mới", StartTime = new(11, 30), EndTime = new(15, 30), ConfigurationVersion = 2, IsActive = true, DisplayOrder = 2 });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", ShiftName = "Ca tối mới", StartTime = new(15, 30), EndTime = new(19, 30), ConfigurationVersion = 2, IsActive = true, DisplayOrder = 3 });

        // Existing Period 1 requirements MUST STILL reference version 1 shift IDs
        Assert.All(period1.StaffingRequirements, req => Assert.Contains(req.ShiftTypeId, new[] { 1, 2, 3 }));

        // Now create Period 2 (which should use the new active shifts 4, 5, 6)
        var model2 = new ShiftScheduling.Features.SchedulePeriods.ViewModels.SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ 2",
            WeekStartDate = new DateOnly(2026, 9, 21),
            AvailabilityDeadline = new DateTime(2026, 9, 17, 12, 0, 0)
        };

        var result2 = await service.CreateAsync(model2, actorAccountId: 1);
        Assert.True(result2.Succeeded);

        var period2 = periodRepo.SchedulePeriods[1];
        Assert.Equal(21, period2.StaffingRequirements.Count);
        Assert.All(period2.StaffingRequirements, req => Assert.Contains(req.ShiftTypeId, new[] { 4, 5, 6 }));
    }
}
