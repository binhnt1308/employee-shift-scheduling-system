namespace ShiftScheduling.Tests.Dashboard;

public sealed class DashboardApprovedLeaveTests
{
    [Fact(
        DisplayName =
            "Dashboard - Approved Leave hiển thị Nghỉ đã duyệt và không tính ca làm")]
    public async Task GetDashboardAsync_ApprovedLeave_ProjectsLeaveInsteadOfWorkingShift()
    {
        var context = CreateApprovedLeaveScenario();

        var result = await context
            .CreateService(new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc))
            .GetDashboardAsync(8);

        Assert.True(result.HasSchedule);
        Assert.True(result.HasMySchedule);
        Assert.Equal(1, result.EmployeeTotalAssignedShifts);

        var saturday = result.EmployeeWeekSchedule.Single(
            day => day.Date == new DateOnly(2026, 10, 3));
        var sunday = result.EmployeeWeekSchedule.Single(
            day => day.Date == new DateOnly(2026, 10, 4));

        Assert.Single(saturday.Shifts);
        Assert.False(saturday.Shifts[0].HasApprovedLeave);

        Assert.Single(sunday.Shifts);
        Assert.True(sunday.Shifts[0].HasApprovedLeave);
        Assert.Equal("Ca tối", sunday.Shifts[0].ShiftName);
    }

    private static DashboardTestContext CreateApprovedLeaveScenario()
    {
        var context = new DashboardTestContext();
        context.AddEmployeeAccount(8, 8, "NV08", "Nguyễn Thị D");

        context.AddShift(
            3,
            "EVENING",
            "Ca tối",
            true,
            1,
            new TimeOnly(16, 0),
            new TimeOnly(20, 0));

        var period = context.AddPeriod(
            10,
            "Kỳ 28/09 - 04/10",
            new DateOnly(2026, 9, 28),
            new DateOnly(2026, 10, 4));

        var schedule = context.AddPublishedSchedule(period, 1);

        context.AddAssignment(
            schedule, 1, 8, new DateOnly(2026, 10, 3), 3);
        context.AddAssignment(
            schedule, 2, 8, new DateOnly(2026, 10, 4), 3);

        context.AddApprovedLeave(
            1,
            10,
            8,
            new DateOnly(2026, 10, 4),
            3);

        return context;
    }
}
