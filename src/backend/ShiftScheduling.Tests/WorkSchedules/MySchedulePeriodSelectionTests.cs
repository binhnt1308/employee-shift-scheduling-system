using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class MySchedulePeriodSelectionTests
{
    private static readonly DateOnly Today = new(2026, 9, 8); // Thứ Ba 08/09/2026

    [Fact(DisplayName = "MS-12: Period selector chứa cả Published và Closed periods")]
    public async Task MySchedule_PeriodSelector_ContainsPublishedAndClosedPeriods()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var pastPeriod = fixture.CreatePeriod(1, "Kỳ quá khứ", new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 6));
        fixture.CreateSchedule(1, pastPeriod, ScheduleStatus.Closed, publishedAt: new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc));

        var currentPeriod = fixture.CreatePeriod(2, "Kỳ hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        fixture.CreateSchedule(2, currentPeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: null);

        Assert.NotNull(result);
        Assert.Contains(result.PeriodOptions, opt => opt.SchedulePeriodId == 1 && opt.Status == ScheduleStatus.Closed);
        Assert.Contains(result.PeriodOptions, opt => opt.SchedulePeriodId == 2 && opt.Status == ScheduleStatus.Published);
    }

    [Fact(DisplayName = "MS-13: Period selector không chứa Draft period")]
    public async Task MySchedule_PeriodSelector_DraftPeriod_ShouldNotBeVisible()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var draftPeriod = fixture.CreatePeriod(1, "Kỳ nháp", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));
        fixture.CreateSchedule(1, draftPeriod, ScheduleStatus.Draft, publishedAt: null);

        var currentPeriod = fixture.CreatePeriod(2, "Kỳ hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        fixture.CreateSchedule(2, currentPeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: null);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.PeriodOptions, opt => opt.SchedulePeriodId == 1);
        Assert.Contains(result.PeriodOptions, opt => opt.SchedulePeriodId == 2);
    }

    [Fact(DisplayName = "MS-14: Closed period vẫn xem được lịch sử của chính mình")]
    public async Task MySchedule_ClosedPeriod_CanBeSelectedAndRendered()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var closedPeriod = fixture.CreatePeriod(1, "Kỳ đã chốt", new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 6));
        var closedSchedule = fixture.CreateSchedule(1, closedPeriod, ScheduleStatus.Closed, publishedAt: new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(closedSchedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 1), 1);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);

        Assert.NotNull(result);
        Assert.Equal(1, result.SchedulePeriodId);
        Assert.Equal(ScheduleStatus.Closed, result.Status);
        var day = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 1));
        var shift = day.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Single(shift.AssignedEmployees);
    }

    [Fact(DisplayName = "MS-15: Employee chọn một kỳ lịch cũ hợp lệ -> data trả đúng kỳ đó")]
    public async Task MySchedule_SpecificValidPeriod_ReturnsDataForThatPeriod()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var oldPeriod = fixture.CreatePeriod(5, "Kỳ cũ hợp lệ", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23));
        var oldSchedule = fixture.CreateSchedule(5, oldPeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(oldSchedule, fixture.CurrentEmployee, new DateOnly(2026, 8, 18), 2);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 5);

        Assert.NotNull(result);
        Assert.Equal(5, result.SchedulePeriodId);
        Assert.Equal(oldPeriod.PeriodName, result.PeriodName);
        var day = result.Days.First(d => d.WorkDate == new DateOnly(2026, 8, 18));
        var shift = day.Shifts.First(s => s.ShiftTypeId == 2);
        Assert.Single(shift.AssignedEmployees);
    }
}
