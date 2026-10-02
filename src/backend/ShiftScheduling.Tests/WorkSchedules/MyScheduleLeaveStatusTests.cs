using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class MyScheduleLeaveStatusTests
{
    private static readonly DateOnly Today = new(2026, 9, 8); // Thứ Ba 08/09/2026

    [Fact(DisplayName = "MS-08: Employee có assignment, không có leave -> MySchedule hiển thị Assigned bình thường")]
    public async Task MySchedule_NoLeave_ShowsAssignedNormal()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var period = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var schedule = fixture.CreateSchedule(1, period, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(schedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);

        Assert.NotNull(result);
        var day = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 8));
        var shift = day.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Single(shift.AssignedEmployees);
        var assignment = shift.AssignedEmployees[0];
        Assert.Equal(fixture.CurrentEmployee.EmployeeId, assignment.EmployeeId);
        Assert.False(assignment.HasApprovedLeave, "Ca làm việc bình thường không được có HasApprovedLeave = true.");
        Assert.Null(assignment.ApprovedLeaveRequestId);
    }

    [Fact(DisplayName = "MS-09: Employee có assignment + Approved Leave đúng date/shift -> MySchedule hiển thị Approved Leave thay vì bình thường")]
    public async Task MySchedule_ApprovedLeave_ShouldShowLeaveInsteadOfAssigned()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var period = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var schedule = fixture.CreateSchedule(1, period, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(schedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1);

        var leave = fixture.AddLeaveRequest(1, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1, LeaveRequestStatus.Approved);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);

        Assert.NotNull(result);
        var day = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 8));
        var shift = day.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Single(shift.AssignedEmployees);
        var assignment = shift.AssignedEmployees[0];
        Assert.True(assignment.HasApprovedLeave, "Ca có đơn nghỉ đã duyệt phải có HasApprovedLeave = true.");
        Assert.Equal(leave.LeaveRequestId, assignment.ApprovedLeaveRequestId);
    }

    [Fact(DisplayName = "MS-10: Approved Leave của employee khác hoặc date khác hoặc shift khác -> không ảnh hưởng assignment hiện tại")]
    public async Task MySchedule_ApprovedLeave_ForOtherEmployeeOrDateOrShift_ShouldNotAffectCurrentAssignment()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var period = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var schedule = fixture.CreateSchedule(1, period, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(schedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1);

        var otherEmployee = new Employee
        {
            EmployeeId = 2,
            UserAccountId = 20,
            EmployeeCode = "NV02",
            FullName = "Đồng Nghiệp B",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };
        fixture.EmployeeRepo.Employees.Add(otherEmployee);

        fixture.AddLeaveRequest(1, otherEmployee, new DateOnly(2026, 9, 8), 1, LeaveRequestStatus.Approved);
        fixture.AddLeaveRequest(1, fixture.CurrentEmployee, new DateOnly(2026, 9, 9), 1, LeaveRequestStatus.Approved);
        fixture.AddLeaveRequest(1, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 2, LeaveRequestStatus.Approved);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);

        Assert.NotNull(result);
        var day = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 8));
        var shift = day.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Single(shift.AssignedEmployees);
        var assignment = shift.AssignedEmployees[0];
        Assert.False(assignment.HasApprovedLeave, "Leave của người khác/ngày khác/ca khác không được làm thay đổi trạng thái ca hiện tại.");
    }

    [Fact(DisplayName = "MS-11A: Rejected Leave -> assignment vẫn hiển thị bình thường")]
    public async Task MySchedule_RejectedLeave_ShouldRemainNormalAssigned()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var period = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var schedule = fixture.CreateSchedule(1, period, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(schedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1);

        fixture.AddLeaveRequest(1, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1, LeaveRequestStatus.Rejected);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);

        Assert.NotNull(result);
        var day = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 8));
        var shift = day.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Single(shift.AssignedEmployees);
        var assignment = shift.AssignedEmployees[0];
        Assert.Equal(fixture.CurrentEmployee.EmployeeId, assignment.EmployeeId);
        Assert.False(assignment.HasApprovedLeave, "Đơn nghỉ bị từ chối không được tính là HasApprovedLeave.");
        Assert.Null(assignment.ApprovedLeaveRequestId);
    }

    [Fact(DisplayName = "MS-11B: Pending Leave -> assignment vẫn hiển thị bình thường")]
    public async Task MySchedule_PendingLeave_ShouldRemainNormalAssigned()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var period = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var schedule = fixture.CreateSchedule(1, period, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(schedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1);

        fixture.AddLeaveRequest(1, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1, LeaveRequestStatus.Pending);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);

        Assert.NotNull(result);
        var day = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 8));
        var shift = day.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Single(shift.AssignedEmployees);
        var assignment = shift.AssignedEmployees[0];
        Assert.Equal(fixture.CurrentEmployee.EmployeeId, assignment.EmployeeId);
        Assert.False(assignment.HasApprovedLeave, "Đơn nghỉ đang chờ duyệt không được tính là HasApprovedLeave.");
        Assert.Null(assignment.ApprovedLeaveRequestId);
    }
}
