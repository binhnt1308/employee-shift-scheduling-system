using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.Dashboard;

public sealed class DashboardServiceTests
{
    [Fact(DisplayName = "TC-DASH-EMP-01 - Employee có phân công")]
    public async Task GetDashboardAsync_WhenEmployeeHasAssignment_ReturnsHasMyScheduleTrue()
    {
        var context = new DashboardTestContext();
        context.AddEmployeeAccount(10, 5, "NV10", "Nguyễn Văn X");

        var period = context.AddPeriod(
            100, "Kỳ 100", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23));
        var schedule = context.AddPublishedSchedule(period, 1);
        context.AddAssignment(
            schedule, 101, 10, new DateOnly(2026, 8, 20), 1);

        var result = await context
            .CreateService(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync(5);

        Assert.NotNull(result);
        Assert.True(result.HasSchedule);
        Assert.True(result.HasMySchedule);
    }

    [Fact(DisplayName = "TC-DASH-EMP-02 - Employee không có phân công")]
    public async Task GetDashboardAsync_WhenEmployeeHasNoAssignment_ReturnsHasMyScheduleFalse()
    {
        var context = new DashboardTestContext();
        context.AddEmployeeAccount(20, 6, "NV20", "Trần Văn Y");

        var period = context.AddPeriod(
            100, "Kỳ 100", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23));
        var schedule = context.AddPublishedSchedule(period, 1);
        context.AddAssignment(
            schedule, 101, 10, new DateOnly(2026, 8, 20), 1);

        var result = await context
            .CreateService(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync(6);

        Assert.NotNull(result);
        Assert.True(result.HasSchedule);
        Assert.False(result.HasMySchedule);
    }

    [Fact(DisplayName = "TC-DASH-ACTIVE-01 - Dashboard chỉ trả ca active")]
    public async Task GetDashboardAsync_OnlyCountsAndReturnsActiveShifts()
    {
        var context = new DashboardTestContext();

        context.AddShift(1, "MORNING", "Ca sáng (v1)", false, 1);
        context.AddShift(2, "AFTERNOON", "Ca chiều (v1)", false, 1);
        context.AddShift(3, "EVENING", "Ca tối (v1)", false, 1);
        context.AddShift(4, "MORNING", "Ca sáng (v2)", true, 2);
        context.AddShift(5, "AFTERNOON", "Ca chiều (v2)", true, 2);
        context.AddShift(6, "EVENING", "Ca tối (v2)", true, 2);

        var result = await context
            .CreateService(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync();

        Assert.NotNull(result);
        Assert.Equal(3, result.ShiftTypeCount);
        Assert.Equal(3, result.ShiftTypes.Count);
        Assert.All(result.ShiftTypes, shift => Assert.True(shift.IsActive));
        Assert.All(
            result.ShiftTypes,
            shift => Assert.Equal(2, shift.ConfigurationVersion));
    }

    [Fact(DisplayName = "TC-DASH-MGR-01 - Kỳ quá hạn cần xử lý")]
    public async Task GetDashboardAsync_WhenPeriodOverdueUnassigned_ReturnsNeedsAttentionTrue()
    {
        var context = new DashboardTestContext();
        var period = context.AddPeriod(
            200, "Kỳ quá hạn", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 16));
        period.AvailabilityDeadline =
            new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        var result = await context
            .CreateService(new DateTime(2026, 8, 12, 5, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync();

        Assert.NotNull(result);
        Assert.Equal("Quá hạn – chưa phân công", result.ScheduleStatusDisplay);
        Assert.True(result.NeedsAttention);
        Assert.NotNull(result.AttentionMessage);
    }

    [Fact(DisplayName = "TC-DASH-EMP-03 - Các trạng thái lịch Employee")]
    public async Task GetDashboardAsync_ForEmployee_ReturnsNeutralStatusScenarios()
    {
        var context = new DashboardTestContext();
        context.AddEmployeeAccount(30, 7, "NV30", "Lê Văn C");

        var period = context.AddPeriod(
            300, "Kỳ hiện tại", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23));
        var service = context.CreateService(
            new DateTime(2026, 8, 18, 5, 0, 0, DateTimeKind.Utc));

        var unpublished = await service.GetDashboardAsync(7);
        Assert.Equal(
            "Lịch làm việc chưa được công bố",
            unpublished.EmployeeScheduleStatusDisplay);
        Assert.Equal(
            "17/08/2026 - 23/08/2026",
            unpublished.TargetPeriodDateRange);

        var schedule = context.AddPublishedSchedule(period, 10);
        var noAssignment = await service.GetDashboardAsync(7);
        Assert.Equal(
            "Chưa có ca được phân công",
            noAssignment.EmployeeScheduleStatusDisplay);

        context.AddAssignment(
            schedule, 501, 30, new DateOnly(2026, 8, 18), 1);
        var assigned = await service.GetDashboardAsync(7);

        Assert.Equal("Đã có lịch làm việc", assigned.EmployeeScheduleStatusDisplay);
        Assert.Equal("Kỳ lịch hiện tại", assigned.EmployeePeriodLabel);
    }

    [Fact(DisplayName = "TC-DASH-EMP-04 - Nhãn kỳ lịch Employee")]
    public async Task GetDashboardAsync_EmployeePeriodLabel_ComputesCorrectLabels()
    {
        var context = new DashboardTestContext();
        context.AddEmployeeAccount(31, 8, "NV31", "Vũ Thị D");

        var noPeriod = await context
            .CreateService(new DateTime(2026, 8, 10, 5, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync(8);
        Assert.Equal("Chưa có kỳ lịch", noPeriod.EmployeePeriodLabel);
        Assert.Equal(
            "Chưa có kỳ lịch",
            noPeriod.EmployeeScheduleStatusDisplay);

        context.AddPeriod(
            301, "Tên kỳ nội bộ quản lý 1",
            new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23));

        var current = await context
            .CreateService(new DateTime(2026, 8, 18, 5, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync(8);
        Assert.Equal("Kỳ lịch hiện tại", current.EmployeePeriodLabel);
        Assert.Equal(
            "Tên kỳ nội bộ quản lý 1",
            current.TargetPeriodName);

        var upcomingContext = new DashboardTestContext();
        upcomingContext.AddEmployeeAccount(31, 8, "NV31", "Vũ Thị D");
        upcomingContext.AddPeriod(
            302, "Tên kỳ nội bộ quản lý 2",
            new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 30));

        var upcoming = await upcomingContext
            .CreateService(new DateTime(2026, 8, 20, 5, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync(8);
        Assert.Equal("Kỳ lịch sắp tới", upcoming.EmployeePeriodLabel);
    }

    [Fact(DisplayName = "TC-DASH-EMP-05 - Dashboard có lịch tuần Employee")]
    public async Task GetDashboardAsync_WhenEmployeeLoggedIn_PopulatesFullNameAndWeekSchedule()
    {
        var context = new DashboardTestContext();
        context.AddEmployeeAccount(50, 50, "NV50", "Nguyễn Văn Test");
        context.AddShift(
            1, "MORNING", "Ca sáng", true, 1,
            new TimeOnly(8, 0), new TimeOnly(12, 0));

        var period = context.AddPeriod(
            500, "Kỳ 500", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var schedule = context.AddPublishedSchedule(period, 500);
        context.AddAssignment(
            schedule, 5001, 50, new DateOnly(2026, 9, 8), 1);
        context.AddAssignment(
            schedule, 5002, 50, new DateOnly(2026, 9, 10), 1);

        var result = await context
            .CreateService(new DateTime(2026, 9, 8, 8, 30, 0, DateTimeKind.Utc))
            .GetDashboardAsync(50);

        Assert.NotNull(result);
        Assert.Equal("Nguyễn Văn Test", result.EmployeeFullName);
        Assert.True(result.HasSchedule);
        Assert.True(result.HasMySchedule);
        Assert.Equal("badge-active", result.EmployeeScheduleBadgeClass);
        Assert.Equal(2, result.EmployeeTotalAssignedShifts);
        Assert.Equal(7, result.EmployeeWeekSchedule.Count);
        Assert.Empty(result.EmployeeWeekSchedule[0].Shifts);
        Assert.False(result.EmployeeWeekSchedule[0].IsToday);
        Assert.True(result.EmployeeWeekSchedule[1].IsToday);
        Assert.Equal("Ca sáng", result.EmployeeWeekSchedule[1].Shifts.Single().ShiftName);
        Assert.Equal(
            "08:00 - 12:00",
            result.EmployeeWeekSchedule[1].Shifts.Single().ShiftHours);
        Assert.Single(result.EmployeeWeekSchedule[3].Shifts);
    }

    [Fact(DisplayName = "TC-DASH-EMP-06 - Badge khi đang mở đăng ký")]
    public async Task GetDashboardAsync_WhenRegistrationOpen_SetsIsRegistrationOpenTrue()
    {
        var context = new DashboardTestContext();
        context.AddEmployeeAccount(60, 60, "NV60", "Trần Thị Test");

        var period = context.AddPeriod(
            600, "Kỳ mở đăng ký",
            new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));
        period.RegistrationStatus = RegistrationStatus.Open;
        period.AvailabilityDeadline =
            new DateTime(2026, 9, 12, 17, 0, 0, DateTimeKind.Utc);

        var result = await context
            .CreateService(new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync(60);

        Assert.NotNull(result);
        Assert.True(result.IsRegistrationOpen);
        Assert.Equal("badge-primary-custom", result.EmployeeScheduleBadgeClass);
        Assert.Equal(
            "Lịch làm việc chưa được công bố",
            result.EmployeeScheduleStatusDisplay);
    }
}
