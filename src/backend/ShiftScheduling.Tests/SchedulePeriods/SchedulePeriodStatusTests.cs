using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Dashboard.Services;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.SchedulePeriods;

public sealed class SchedulePeriodStatusTests
{
    [Fact(DisplayName =
        "TC-LC-001: Có lịch chưa công bố và kỳ đã bắt đầu -> Status 'Bản nháp' và Cần xử lý")]
    public void CalculateEffectiveStatus_DraftScheduleAndPeriodStarted_ReturnsBanNhapWithWarning()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1, PeriodName = "Kỳ 1",
            WeekStartDate = new DateOnly(2026, 9, 1), WeekEndDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 8, 30, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        var workSchedule = new WorkSchedule
        {
            WorkScheduleId = 10, SchedulePeriodId = 1, Status = ScheduleStatus.Draft
        };
        var currentUtc = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 2);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            period, workSchedule, currentUtc, currentDate);

        Assert.Equal("Bản nháp", result.StatusName);
        Assert.True(result.NeedsAttention);
        Assert.Equal("Kỳ lịch đã bắt đầu nhưng lịch làm việc chưa được công bố.", result.AttentionMessage);
    }

    [Fact(DisplayName =
        "TC-LC-002: Không có lịch và kỳ đã bắt đầu -> Status 'Quá hạn – chưa phân công' và Cần xử lý")]
    public void CalculateEffectiveStatus_NoScheduleAndPeriodStarted_ReturnsOverdueUnassignedWithWarning()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1, PeriodName = "Kỳ 1",
            WeekStartDate = new DateOnly(2026, 9, 1), WeekEndDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 8, 30, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        var currentUtc = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 2);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, null, currentUtc, currentDate);

        Assert.Equal("Quá hạn – chưa phân công", result.StatusName);
        Assert.True(result.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-003: Có lịch đã công bố và kỳ đang diễn ra -> Status 'Đang áp dụng'")]
    public void CalculateEffectiveStatus_PublishedScheduleAndOngoing_ReturnsDangApDung()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 9, 1), WeekEndDate = new DateOnly(2026, 9, 7)
        };
        var workSchedule = new WorkSchedule { WorkScheduleId = 10, Status = ScheduleStatus.Published };
        var currentUtc = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 3);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            period, workSchedule, currentUtc, currentDate);

        Assert.Equal("Đang áp dụng", result.StatusName);
        Assert.False(result.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-004: Kỳ kết thúc có lịch công bố chưa chốt -> Status 'Đang áp dụng'")]
    public void CalculateEffectiveStatus_EndedPeriodWithPublishedSchedule_ReturnsDangApDung()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 1), WeekEndDate = new DateOnly(2026, 8, 7)
        };
        var workSchedule = new WorkSchedule { WorkScheduleId = 10, Status = ScheduleStatus.Published };
        var currentUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 1);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            period, workSchedule, currentUtc, currentDate);

        Assert.Equal("Đang áp dụng", result.StatusName);
    }

    [Fact(DisplayName =
        "TC-LC-005: Kỳ kết thúc không có lịch -> Status 'Đã kết thúc – chưa phân công'")]
    public void CalculateEffectiveStatus_EndedPeriodNoSchedule_ReturnsEndedUnassigned()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 1), WeekEndDate = new DateOnly(2026, 8, 7)
        };
        var currentUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 1);

        var result = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, null, currentUtc, currentDate);

        Assert.Equal("Đã kết thúc – chưa phân công", result.StatusName);
    }

    [Fact(DisplayName =
        "TC-LC-010: Dashboard chọn kỳ đang diễn ra thay vì lịch công bố cũ của kỳ khác")]
    public async Task GetDashboardAsync_OngoingPeriodExists_SelectsOngoingPeriodOverOldPublishedSchedule()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var oldPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 1, PeriodName = "Kỳ cũ",
            WeekStartDate = new DateOnly(2026, 8, 1), WeekEndDate = new DateOnly(2026, 8, 7)
        };
        oldPeriod.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 100, SchedulePeriodId = 1, Status = ScheduleStatus.Published
        });
        periodRepo.Add(oldPeriod);

        var ongoingPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 2, PeriodName = "Kỳ hiện tại",
            WeekStartDate = new DateOnly(2026, 9, 1), WeekEndDate = new DateOnly(2026, 9, 7)
        };
        periodRepo.Add(ongoingPeriod);

        var clock = new FakeClock(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(
            new FakeAccountRepository(), new FakeEmployeeRepository(), new FakeShiftTypeRepository(),
            periodRepo, clock);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal("Kỳ hiện tại", dashboard.TargetPeriodName);
        Assert.Equal("Quá hạn – chưa phân công", dashboard.ScheduleStatusDisplay);
        Assert.True(dashboard.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-011: Không có kỳ hiện tại -> Chọn kỳ sắp tới gần nhất")]
    public async Task GetDashboardAsync_NoOngoingPeriod_SelectsNearestUpcomingPeriod()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var upcomingPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 2, PeriodName = "Kỳ tuần sau",
            WeekStartDate = new DateOnly(2026, 9, 14), WeekEndDate = new DateOnly(2026, 9, 20),
            AvailabilityDeadline = new DateTime(2026, 9, 12, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        periodRepo.Add(upcomingPeriod);

        var clock = new FakeClock(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(
            new FakeAccountRepository(), new FakeEmployeeRepository(), new FakeShiftTypeRepository(),
            periodRepo, clock);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal("Kỳ tuần sau", dashboard.TargetPeriodName);
        Assert.Equal("Đang mở đăng ký", dashboard.ScheduleStatusDisplay);
        Assert.False(dashboard.NeedsAttention);
    }

    [Fact(DisplayName = "TC-LC-012: Target period có lịch công bố -> Dashboard hiện 'Đang áp dụng'")]
    public async Task GetDashboardAsync_TargetPeriodPublished_ReturnsDangApDungStatus()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var ongoingPeriod = new SchedulePeriod
        {
            SchedulePeriodId = 2, PeriodName = "Kỳ 2",
            WeekStartDate = new DateOnly(2026, 9, 1), WeekEndDate = new DateOnly(2026, 9, 7)
        };
        ongoingPeriod.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 200, SchedulePeriodId = 2, Status = ScheduleStatus.Published
        });
        periodRepo.Add(ongoingPeriod);

        var clock = new FakeClock(new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));
        var service = new DashboardService(
            new FakeAccountRepository(), new FakeEmployeeRepository(), new FakeShiftTypeRepository(),
            periodRepo, clock);

        var dashboard = await service.GetDashboardAsync();

        Assert.Equal("Kỳ 2", dashboard.TargetPeriodName);
        Assert.Equal("Đang áp dụng", dashboard.ScheduleStatusDisplay);
        Assert.True(dashboard.HasSchedule);
    }

    [Fact(DisplayName = "TC-LC-018: Kiểm tra DisplayBadgeClass cho kỳ đã qua có lịch và không có lịch")]
    public void CalculateEffectiveStatus_EndedPeriod_ReturnsCorrectBadgeClass()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1, PeriodName = "Kỳ đã qua",
            WeekStartDate = new DateOnly(2026, 8, 3), WeekEndDate = new DateOnly(2026, 8, 9)
        };
        var currentUtc = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 8, 15);

        var publishedSchedule = new WorkSchedule { WorkScheduleId = 10, Status = ScheduleStatus.Published };
        var statusA = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            period, publishedSchedule, currentUtc, currentDate);
        Assert.Equal("Đang áp dụng", statusA.StatusName);
        Assert.Equal("badge-active", statusA.DisplayBadgeClass);

        var statusB = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            period, null, currentUtc, currentDate);
        Assert.Equal("Đã kết thúc – chưa phân công", statusB.StatusName);
        Assert.Equal("badge-ended", statusB.DisplayBadgeClass);
    }

    [Fact(DisplayName = "TC-LC-020: IsRegistrationEffectivelyOpen trả true khi Open và chưa hết deadline")]
    public void IsRegistrationEffectivelyOpen_WhenOpenAndBeforeDeadlineAndBeforeStartDate_ReturnsTrue()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1, RegistrationStatus = RegistrationStatus.Open,
            WeekStartDate = new DateOnly(2026, 9, 7),
            AvailabilityDeadline = new DateTime(2026, 9, 5, 23, 59, 0, DateTimeKind.Utc)
        };
        var currentUtc = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 4);

        var isOpen = SchedulePeriodStatusHelper.IsRegistrationEffectivelyOpen(period, currentUtc, currentDate);
        Assert.True(isOpen);
    }

    [Fact(DisplayName =
        "TC-LC-021: IsRegistrationEffectivelyOpen trả false khi kỳ đã bắt đầu dù DB lưu Open")]
    public void IsRegistrationEffectivelyOpen_WhenPeriodStartedEvenIfDeadlineInFuture_ReturnsFalse()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1, RegistrationStatus = RegistrationStatus.Open,
            WeekStartDate = new DateOnly(2026, 8, 31),
            AvailabilityDeadline = new DateTime(2026, 9, 6, 23, 59, 0, DateTimeKind.Utc)
        };
        var currentUtc = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var currentDate = new DateOnly(2026, 9, 2);

        var isOpen = SchedulePeriodStatusHelper.IsRegistrationEffectivelyOpen(period, currentUtc, currentDate);
        Assert.False(isOpen);
    }
}
