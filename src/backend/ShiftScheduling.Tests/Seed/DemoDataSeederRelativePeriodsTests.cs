using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Infrastructure.Persistence.Seed;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.Seed;

public sealed class DemoDataSeederRelativePeriodsTests
{
    private readonly IPasswordHashService _passwordHash = new Pbkdf2PasswordHashService();
    private readonly SchedulingValidationService _validationService = new();

    private sealed class TestClock : IClock
    {
        private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);
        public DateTime UtcNow { get; }
        public DateOnly TodayInVietnam => DateOnly.FromDateTime(ToVietnamTime(UtcNow));

        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public static TestClock FromVietnamDate(int year, int month, int day, int hour = 9, int minute = 0)
        {
            var vnLocal = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
            var utc = vnLocal - VnOffset;
            return new TestClock(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        }

        public DateTime ToVietnamTime(DateTime utcDateTime) => utcDateTime + VnOffset;
        public DateTime ToUtcFromVietnamTime(DateTime vietnamDateTime) => vietnamDateTime - VnOffset;
    }

    [Fact(DisplayName = "TC-SEEDREL-001: Tính toán 4 mốc Thứ Hai đúng cho ngày kiểm thử 16/09/2026")]
    public void TC_REL_01_SeptemberDate_CalculatesCorrectFourPeriodStarts()
    {
        var testClock = TestClock.FromVietnamDate(2026, 9, 16);
        var today = testClock.TodayInVietnam;

        var (prevStart, currStart, nextStart, followStart) = DemoDataSeeder.CalculateFourPeriodDates(today);

        Assert.Equal(new DateOnly(2026, 9, 7), prevStart);
        Assert.Equal(new DateOnly(2026, 9, 14), currStart);
        Assert.Equal(new DateOnly(2026, 9, 21), nextStart);
        Assert.Equal(new DateOnly(2026, 9, 28), followStart);

        var dataSet = DemoDataSeeder.BuildFourPeriodDemoDataSet(today, _passwordHash, testClock);
        Assert.Equal(4, dataSet.Periods.Count);

        var p1 = dataSet.Periods.Single(p => p.WeekStartDate == prevStart);
        Assert.Equal(RegistrationStatus.Closed, p1.RegistrationStatus);
        var ws1 = dataSet.WorkSchedules.Single(ws => ws.SchedulePeriod == p1);
        Assert.Equal(ScheduleStatus.Closed, ws1.Status);

        var p2 = dataSet.Periods.Single(p => p.WeekStartDate == currStart);
        Assert.Equal(RegistrationStatus.Closed, p2.RegistrationStatus);
        var ws2 = dataSet.WorkSchedules.Single(ws => ws.SchedulePeriod == p2);
        Assert.Equal(ScheduleStatus.Published, ws2.Status);

        var p3 = dataSet.Periods.Single(p => p.WeekStartDate == nextStart);
        Assert.Equal(RegistrationStatus.Open, p3.RegistrationStatus);
        Assert.True(p3.AvailabilityDeadline < testClock.UtcNow);
        var ws3 = dataSet.WorkSchedules.Single(ws => ws.SchedulePeriod == p3);
        Assert.Equal(ScheduleStatus.Draft, ws3.Status);

        var p4 = dataSet.Periods.Single(p => p.WeekStartDate == followStart);
        Assert.Equal(RegistrationStatus.Open, p4.RegistrationStatus);
        Assert.True(p4.AvailabilityDeadline > testClock.UtcNow);
        Assert.DoesNotContain(dataSet.WorkSchedules, ws => ws.SchedulePeriod == p4);
    }

    [Fact(DisplayName = "TC-SEEDREL-002: Fresh seed ngày 15/10/2026 tạo 4 kỳ đúng dải ngày Tháng 10")]
    public void TC_REL_02_OctoberDemo_CalculatesCorrectOctoberPeriods()
    {
        var testClock = TestClock.FromVietnamDate(2026, 10, 15);
        var today = testClock.TodayInVietnam;

        var (prevStart, currStart, nextStart, followStart) = DemoDataSeeder.CalculateFourPeriodDates(today);

        Assert.Equal(new DateOnly(2026, 10, 5), prevStart);
        Assert.Equal(new DateOnly(2026, 10, 12), currStart);
        Assert.Equal(new DateOnly(2026, 10, 19), nextStart);
        Assert.Equal(new DateOnly(2026, 10, 26), followStart);

        var dataSet = DemoDataSeeder.BuildFourPeriodDemoDataSet(today, _passwordHash, testClock);
        var periods = dataSet.Periods.OrderBy(p => p.WeekStartDate).ToList();

        Assert.Equal(new DateOnly(2026, 10, 5), periods[0].WeekStartDate);
        Assert.Equal(new DateOnly(2026, 10, 11), periods[0].WeekEndDate);

        Assert.Equal(new DateOnly(2026, 10, 12), periods[1].WeekStartDate);
        Assert.Equal(new DateOnly(2026, 10, 18), periods[1].WeekEndDate);

        Assert.Equal(new DateOnly(2026, 10, 19), periods[2].WeekStartDate);
        Assert.Equal(new DateOnly(2026, 10, 25), periods[2].WeekEndDate);

        Assert.Equal(new DateOnly(2026, 10, 26), periods[3].WeekStartDate);
        Assert.Equal(new DateOnly(2026, 11, 1), periods[3].WeekEndDate);
    }

    [Fact(DisplayName = "TC-SEEDREL-003: Fresh seed cuối tháng 31/10/2026 vượt biên tháng chính xác")]
    public void TC_REL_03_MonthBoundary_CalculatesCorrectCrossMonthPeriods()
    {
        var testClock = TestClock.FromVietnamDate(2026, 10, 31);
        var today = testClock.TodayInVietnam;

        var (prevStart, currStart, nextStart, followStart) = DemoDataSeeder.CalculateFourPeriodDates(today);

        Assert.Equal(new DateOnly(2026, 10, 19), prevStart);
        Assert.Equal(new DateOnly(2026, 10, 26), currStart);
        Assert.Equal(new DateOnly(2026, 11, 2), nextStart);
        Assert.Equal(new DateOnly(2026, 11, 9), followStart);

        var dataSet = DemoDataSeeder.BuildFourPeriodDemoDataSet(today, _passwordHash, testClock);
        Assert.Equal(4, dataSet.Periods.Count);
    }

    [Fact(DisplayName = "TC-SEEDREL-004: Period 2 hiển thị Đang áp dụng qua helper, không persist Active")]
    public void TC_REL_04_CurrentWeek_DerivesActiveStatus_WithoutPersistingActive()
    {
        var testClock = TestClock.FromVietnamDate(2026, 10, 15);
        var today = testClock.TodayInVietnam;
        var dataSet = DemoDataSeeder.BuildFourPeriodDemoDataSet(today, _passwordHash, testClock);

        var currentPeriod = dataSet.Periods.Single(p => p.WeekStartDate == new DateOnly(2026, 10, 12));
        var currentSchedule = dataSet.WorkSchedules.Single(ws => ws.SchedulePeriod == currentPeriod);

        Assert.Equal(ScheduleStatus.Published, currentSchedule.Status);

        var effectiveStatus = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            currentPeriod, currentSchedule, testClock.UtcNow, today);
        Assert.Equal("Đang áp dụng", effectiveStatus.StatusName);
        Assert.Equal("badge-active", effectiveStatus.DisplayBadgeClass);
    }

    [Fact(DisplayName = "TC-SEEDREL-005: Period 3 sẵn sàng xếp lịch và giải được bằng OR-Tools solver")]
    public void TC_REL_05_Period3_IsReadyToSchedule_AndOrToolsSolvable()
    {
        var testClock = TestClock.FromVietnamDate(2026, 10, 15);
        var today = testClock.TodayInVietnam;
        var dataSet = DemoDataSeeder.BuildFourPeriodDemoDataSet(today, _passwordHash, testClock);

        var period3 = dataSet.Periods.Single(p => p.WeekStartDate == new DateOnly(2026, 10, 19));
        var ws3 = dataSet.WorkSchedules.Single(ws => ws.SchedulePeriod == period3);
        Assert.Equal(ScheduleStatus.Draft, ws3.Status);

        var reqs3 = dataSet.StaffingRequirements.Where(r => r.SchedulePeriod == period3).ToList();
        var avails3 = dataSet.Availabilities.Where(a => a.SchedulePeriod == period3).ToList();
        for (int i = 0; i < dataSet.Employees.Count; i++) dataSet.Employees[i].EmployeeId = i + 1;
        foreach (var av in avails3) av.EmployeeId = av.Employee.EmployeeId;

        var shiftTypes = new List<ShiftType>
        {
            new()
            {
                ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4, IsActive = true,
                StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), MaxShiftsPerEmployeePerDay = 2
            },
            new()
            {
                ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4, IsActive = true,
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), MaxShiftsPerEmployeePerDay = 2
            },
            new()
            {
                ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4, IsActive = true,
                StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), MaxShiftsPerEmployeePerDay = 2
            }
        };

        var orTools = new OrToolsSchedulingService(
            _validationService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OrToolsSchedulingService>.Instance);
        var proposal = orTools.GenerateProposal(
            period3, dataSet.Employees, avails3, new List<LeaveRequest>(), reqs3, shiftTypes, 5);

        Assert.True(proposal.Succeeded);
        Assert.NotEmpty(proposal.ProposedAssignments);
        Assert.True(proposal.ValidationResult.IsValid);
    }

    [Fact(DisplayName = "TC-SEEDREL-006: Period 4 không có WorkSchedule và khóa phân công sớm")]
    public void TC_REL_06_Period4_HasNoWorkSchedule_AndBlocksEarlyScheduling()
    {
        var testClock = TestClock.FromVietnamDate(2026, 10, 15);
        var today = testClock.TodayInVietnam;
        var dataSet = DemoDataSeeder.BuildFourPeriodDemoDataSet(today, _passwordHash, testClock);

        var period4 = dataSet.Periods.Single(p => p.WeekStartDate == new DateOnly(2026, 10, 26));

        Assert.Equal(RegistrationStatus.Open, period4.RegistrationStatus);
        Assert.True(period4.AvailabilityDeadline > testClock.UtcNow);
        Assert.DoesNotContain(dataSet.WorkSchedules, ws => ws.SchedulePeriod == period4);

        // Cơ chế đăng ký đang mở hiệu lực
        Assert.True(SchedulePeriodStatusHelper.IsRegistrationEffectivelyOpen(period4, testClock.UtcNow, today));
    }

    [Fact(
        DisplayName = "TC-SEEDREL-007: Restart an toàn - Precondition nhận diện 4 kỳ và bỏ qua không ghi đè")]
    public void TC_REL_07_RestartSafety_PreconditionSkipsExistingFourPeriodSeed()
    {
        var testClock = TestClock.FromVietnamDate(2026, 10, 15);
        var today = testClock.TodayInVietnam;
        var dataSet = DemoDataSeeder.BuildFourPeriodDemoDataSet(today, _passwordHash, testClock);

        var periodNames = dataSet.Periods.Select(p => p.PeriodName).ToList();

        // Database đã có đủ 4 kỳ demo -> SkipAlreadySeeded
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingPeriodNames: periodNames);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decision);
        Assert.True(DemoDataSeeder.IsAlreadySeeded(DemoDataSeeder.DemoEmployeeCodes, periodNames));
    }
}
