using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class MyScheduleWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 9, 8); // Thứ Ba 08/09/2026

    [Fact(DisplayName = "MS-01: Có Published schedule bao phủ today + có Published schedule tương lai publish mới hơn -> mặc định trả lịch bao phủ today")]
    public async Task MS01_WhenCurrentAndFuturePublishedExist_ReturnsCurrentPeriodCoveringToday()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);

        // Kỳ 1: Tuần hiện tại bao phủ today (07/09/2026 - 13/09/2026), publish lúc 05/09/2026
        var currentPeriod = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var currentSchedule = fixture.CreateSchedule(1, currentPeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(currentSchedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1);

        // Kỳ 2: Tuần sau tương lai (14/09/2026 - 20/09/2026), vừa mới publish lúc 08/09/2026 (PublishedAt mới hơn Kỳ 1)
        var futurePeriod = fixture.CreatePeriod(2, "Tuần sau", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));
        var futureSchedule = fixture.CreateSchedule(2, futurePeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 8, 8, 0, 0, DateTimeKind.Utc));
        fixture.AddAssignment(futureSchedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 15), 1);

        // Gọi không truyền schedulePeriodId (mặc định)
        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: null);

        Assert.NotNull(result);
        Assert.Equal(currentPeriod.SchedulePeriodId, result.SchedulePeriodId);
        Assert.Equal(currentPeriod.PeriodName, result.PeriodName);
        Assert.Equal(currentPeriod.WeekStartDate, result.WeekStartDate);
    }

    [Fact(DisplayName = "MS-02: Không có lịch current + có nhiều lịch Published tương lai -> trả lịch tương lai có WeekStartDate gần nhất")]
    public async Task MS02_WhenNoCurrentSchedule_ReturnsNearestFuturePublishedSchedule()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today); // 08/09/2026

        // Kỳ xa hơn: 21/09 - 27/09
        var farFuturePeriod = fixture.CreatePeriod(2, "Tuần xa hơn", new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27));
        fixture.CreateSchedule(2, farFuturePeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));

        // Kỳ gần hơn: 14/09 - 20/09 (WeekStartDate gần nhất)
        var nearFuturePeriod = fixture.CreatePeriod(1, "Tuần tới gần nhất", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));
        fixture.CreateSchedule(1, nearFuturePeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc));

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: null);

        Assert.NotNull(result);
        Assert.Equal(nearFuturePeriod.SchedulePeriodId, result.SchedulePeriodId);
        Assert.Equal(nearFuturePeriod.PeriodName, result.PeriodName);
    }

    [Fact(DisplayName = "MS-03: Không có current/future + có lịch Published quá khứ -> trả lịch quá khứ gần nhất")]
    public async Task MS03_WhenNoCurrentOrFuture_ReturnsNearestPastPublishedSchedule()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today); // 08/09/2026

        // Kỳ quá khứ xa: 24/08 - 30/08
        var olderPastPeriod = fixture.CreatePeriod(1, "Tuần quá khứ cũ", new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 30));
        fixture.CreateSchedule(1, olderPastPeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));

        // Kỳ quá khứ gần nhất: 31/08 - 06/09 (WeekEndDate gần today nhất)
        var nearPastPeriod = fixture.CreatePeriod(2, "Tuần quá khứ gần nhất", new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 6));
        fixture.CreateSchedule(2, nearPastPeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc));

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: null);

        Assert.NotNull(result);
        Assert.Equal(nearPastPeriod.SchedulePeriodId, result.SchedulePeriodId);
        Assert.Equal(nearPastPeriod.PeriodName, result.PeriodName);
    }

    [Fact(DisplayName = "MS-04: Truyền schedulePeriodId cụ thể -> trả đúng Published schedule của period đó")]
    public async Task MS04_WhenPeriodIdSpecified_ReturnsRequestedPublishedScheduleDirectly()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);

        // Kỳ 1 (tuần hiện tại)
        var currentPeriod = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        fixture.CreateSchedule(1, currentPeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));

        // Kỳ 2 (tuần sau)
        var futurePeriod = fixture.CreatePeriod(2, "Tuần sau", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));
        fixture.CreateSchedule(2, futurePeriod, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));

        // Yêu cầu xem cụ thể Kỳ 2
        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 2);

        Assert.NotNull(result);
        Assert.Equal(2, result.SchedulePeriodId);
        Assert.Equal(futurePeriod.PeriodName, result.PeriodName);
    }

    [Fact(DisplayName = "MS-05: Employee chỉ thấy assignments của chính mình")]
    public async Task MS05_EmployeeOnlySeesTheirOwnAssignmentsInGrid()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var period = fixture.CreatePeriod(1, "Tuần hiện tại", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        var schedule = fixture.CreateSchedule(1, period, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));

        // Phân công cho CurrentEmployee (EmployeeId = 1) vào Thứ Ba (08/09)
        fixture.AddAssignment(schedule, fixture.CurrentEmployee, new DateOnly(2026, 9, 8), 1);

        // Phân công cho đồng nghiệp khác (EmployeeId = 2) vào Thứ Tư (09/09)
        var otherEmployee = new Employee
        {
            EmployeeId = 2,
            UserAccountId = 20,
            EmployeeCode = "NV02",
            FullName = "Đồng Nghiệp B",
            Email = "dongnghiep@example.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };
        fixture.EmployeeRepo.Employees.Add(otherEmployee);
        fixture.AddAssignment(schedule, otherEmployee, new DateOnly(2026, 9, 9), 1);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);

        Assert.NotNull(result);

        // Ngày 08/09 phải chứa đúng ca của nhân viên đang đăng nhập, không lẫn ca người khác.
        var tuesday = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 8));
        var tuesdayMorningShift = tuesday.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Single(tuesdayMorningShift.AssignedEmployees);
        Assert.Equal(fixture.CurrentEmployee.EmployeeId, tuesdayMorningShift.AssignedEmployees[0].EmployeeId);

        // Kiểm tra Thứ Tư (09/09) ca của đồng nghiệp B KHÔNG hiển thị trong lưới của CurrentEmployee
        var wednesday = result.Days.First(d => d.WorkDate == new DateOnly(2026, 9, 9));
        var wednesdayMorningShift = wednesday.Shifts.First(s => s.ShiftTypeId == 1);
        Assert.Empty(wednesdayMorningShift.AssignedEmployees);
    }

    [Fact(DisplayName = "MS-06: Draft/Proposed không được dùng làm MySchedule")]
    public async Task MS06_DraftOrProposedScheduleIsNotReturnedAsMySchedule()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);

        // Kỳ chỉ có lịch ở trạng thái Draft
        var period = fixture.CreatePeriod(1, "Tuần nháp", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        fixture.CreateSchedule(1, period, ScheduleStatus.Draft, publishedAt: null);

        var result = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: null);

        // Phải trả về null vì không có Published schedule
        Assert.Null(result);

        // Kể cả khi gọi đích danh periodId=1 thì Draft cũng không được trả về
        var resultSpecific = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1);
        Assert.Null(resultSpecific);
    }

    [Fact(DisplayName = "MS-07: Kỳ lịch dùng versioned ShiftTypes (7, 8, 9) thay vì bộ cũ (1, 2, 3) -> MySchedule hiển thị đúng ca chiều của nhân viên")]
    public async Task MS07_WhenPeriodUsesVersionedShiftTypes_DisplaysEmployeeAssignmentsCorrectly()
    {
        var fixture = MyScheduleTestFixtureFactory.CreateFixture(Today);
        var monday = new DateOnly(2026, 9, 7);
        SetupVersionedShiftPeriod(fixture, monday);

        // 1. Gọi mặc định (schedulePeriodId = null) -> giải quyết kỳ bao phủ today
        var resultDefault = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: null);

        Assert.NotNull(resultDefault);
        Assert.Equal(1002, resultDefault.SchedulePeriodId);

        var mondayDay = resultDefault.Days.First(d => d.WorkDate == monday);
        var afternoonShift = mondayDay.Shifts.FirstOrDefault(s => s.ShiftTypeId == 8);
        Assert.NotNull(afternoonShift);
        Assert.Single(afternoonShift.AssignedEmployees);
        Assert.Equal(fixture.CurrentEmployee.EmployeeId, afternoonShift.AssignedEmployees[0].EmployeeId);

        // 2. Gọi đích danh periodId = 1002
        var resultSpecific = await fixture.Service.GetEmployeeMyScheduleAsync(fixture.CurrentAccount.UserAccountId, schedulePeriodId: 1002);
        Assert.NotNull(resultSpecific);
        var mondayDaySpecific = resultSpecific.Days.First(d => d.WorkDate == monday);
        var afternoonShiftSpecific = mondayDaySpecific.Shifts.FirstOrDefault(s => s.ShiftTypeId == 8);
        Assert.NotNull(afternoonShiftSpecific);
        Assert.Single(afternoonShiftSpecific.AssignedEmployees);
        Assert.Equal(fixture.CurrentEmployee.EmployeeId, afternoonShiftSpecific.AssignedEmployees[0].EmployeeId);
    }

    private static void SetupVersionedShiftPeriod(MyScheduleFixture fixture, DateOnly monday)
    {
        var v2Morning = new ShiftType { ShiftTypeId = 7, ShiftCode = "MORNING", ShiftName = "Ca Sáng V2", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DisplayOrder = 1, IsActive = true };
        var v2Afternoon = new ShiftType { ShiftTypeId = 8, ShiftCode = "AFTERNOON", ShiftName = "Ca Chiều V2", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DisplayOrder = 2, IsActive = true };
        var v2Evening = new ShiftType { ShiftTypeId = 9, ShiftCode = "EVENING", ShiftName = "Ca Tối V2", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DisplayOrder = 3, IsActive = true };
        fixture.ShiftTypeRepo.ShiftTypes.AddRange([v2Morning, v2Afternoon, v2Evening]);

        var period = fixture.CreatePeriod(1002, "kỳ lịch test", new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13));
        for (int i = 0; i < 7; i++)
        {
            var date = period.WeekStartDate.AddDays(i);
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = (i * 3) + 1, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 7, ShiftType = v2Morning, RequiredSalesStaff = 1, RequiredCashiers = 1 });
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = (i * 3) + 2, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 8, ShiftType = v2Afternoon, RequiredSalesStaff = 1, RequiredCashiers = 1 });
            period.StaffingRequirements.Add(new StaffingRequirement { StaffingRequirementId = (i * 3) + 3, SchedulePeriodId = period.SchedulePeriodId, WorkDate = date, ShiftTypeId = 9, ShiftType = v2Evening, RequiredSalesStaff = 1, RequiredCashiers = 1 });
        }

        var schedule = fixture.CreateSchedule(2, period, ScheduleStatus.Published, publishedAt: new DateTime(2026, 9, 8, 9, 0, 0, DateTimeKind.Utc));
        schedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 1124,
            WorkScheduleId = schedule.WorkScheduleId,
            WorkSchedule = schedule,
            EmployeeId = fixture.CurrentEmployee.EmployeeId,
            Employee = fixture.CurrentEmployee,
            WorkDate = monday,
            ShiftTypeId = 8,
            ShiftType = v2Afternoon,
            Source = AssignmentSource.Manual
        });
    }
}
