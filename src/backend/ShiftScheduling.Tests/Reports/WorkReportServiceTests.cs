using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Reports;

public sealed class WorkReportServiceTests
{
    [Fact(DisplayName = "TC11-01 - UC11 BR18 - Tính toán tổng hợp số ca, ngày và giờ làm" +
                        " từ lịch chính thức đã công bố")]
    public async Task GetManagerReportOverviewAsync_WhenPublishedScheduleExists_CalculatesWorkloadCorrectly()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = CreatePeriod(1, "Kỳ lịch 33");
        periodRepo.Add(period);

        var (emp1, emp2) = CreateTwoEmployees();
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        var (shift1, shift2) = CreateStandardFourHourShifts();
        var publishedSchedule = BuildScheduleForTwoEmployees(period, emp1, emp2, shift1, shift2);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = new WorkReportService(workRepo, periodRepo, empRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        Assert.Equal(2, report.Rows.Count);

        var row1 = report.Rows.First(r => r.EmployeeId == 1);
        Assert.Equal(5, row1.TotalShifts);
        Assert.Equal(5, row1.TotalWorkDays);
        Assert.Equal(20m, row1.TotalHours);
        Assert.Equal("Đạt mức dự kiến", row1.Warning);

        var row2 = report.Rows.First(r => r.EmployeeId == 2);
        Assert.Equal(12, row2.TotalShifts);
        Assert.Equal(6, row2.TotalWorkDays);
        Assert.Equal(48m, row2.TotalHours);
        Assert.Equal("Đạt mức dự kiến", row2.Warning);
    }

    [Fact(DisplayName = "TC11-02 - UC11 - Xuất CSV báo cáo có đúng header và dòng dữ liệu")]
    public async Task GenerateReportCsvAsync_ReturnsValidCsvFile()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = CreatePeriod(1, "Kỳ lịch 33");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var (shift1, _) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            EmployeeId = 1,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 1,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = new WorkReportService(workRepo, periodRepo, empRepo);

        var bytes = await service.GenerateReportCsvAsync(1);
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("STT,Mã NV,Họ tên,Vị trí,Hình thức,Số ca,Số ngày,Tổng giờ,Cảnh báo", text);
        Assert.Contains("NV001", text);
        Assert.Contains("Nguyễn Văn A", text);
    }

    [Fact(DisplayName = "TC11-03 - UC11 - Tính tổng giờ theo DurationHours thực tế (decimal) thay vì giả định 4 giờ")]
    public async Task GetManagerReportOverviewAsync_WithNonStandardShiftDurations_SumsActualDurationHoursDecimal()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = CreatePeriod(1, "Kỳ lịch thử nghiệm");
        periodRepo.Add(period);

        var emp = CreateNonStandardEmployee();
        empRepo.Add(emp);

        var publishedSchedule = BuildNonStandardSchedule(period, emp);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = new WorkReportService(workRepo, periodRepo, empRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var row = Assert.Single(report.Rows);
        Assert.Equal(2, row.TotalShifts);
        Assert.Equal(2, row.TotalWorkDays);
        Assert.Equal(7.5m, row.TotalHours);
    }

    private static SchedulePeriod CreatePeriod(int id, string name) => new()
    {
        SchedulePeriodId = id,
        PeriodName = name,
        WeekStartDate = new DateOnly(2026, 8, 10),
        WeekEndDate = new DateOnly(2026, 8, 16)
    };

    private static (Employee Emp1, Employee Emp2) CreateTwoEmployees()
    {
        var emp1 = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV001",
            FullName = "Nguyễn Văn A",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            IsActive = true
        };
        var emp2 = new Employee
        {
            EmployeeId = 2,
            EmployeeCode = "NV002",
            FullName = "Trần Thị B",
            Position = EmployeePosition.Cashier,
            EmploymentType = EmploymentType.FullTime,
            IsActive = true
        };
        return (emp1, emp2);
    }

    private static Employee CreateNonStandardEmployee() => new()
    {
        EmployeeId = 1,
        EmployeeCode = "NV099",
        FullName = "Lê Văn C",
        Position = EmployeePosition.Sales,
        EmploymentType = EmploymentType.PartTime,
        IsActive = true
    };

    private static (ShiftType Shift1, ShiftType Shift2) CreateStandardFourHourShifts()
    {
        var shift1 = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca Sáng",
            DurationHours = 4m
        };
        var shift2 = new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca Chiều",
            DurationHours = 4m
        };
        return (shift1, shift2);
    }

    private static WorkSchedule BuildScheduleForTwoEmployees(
        SchedulePeriod period,
        Employee emp1,
        Employee emp2,
        ShiftType shift1,
        ShiftType shift2)
    {
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Published
        };

        for (int d = 0; d < 5; d++)
        {
            publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
            {
                EmployeeId = emp1.EmployeeId,
                Employee = emp1,
                WorkDate = period.WeekStartDate.AddDays(d),
                ShiftTypeId = shift1.ShiftTypeId,
                ShiftType = shift1
            });
        }

        for (int d = 0; d < 6; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
            {
                EmployeeId = emp2.EmployeeId,
                Employee = emp2,
                WorkDate = date,
                ShiftTypeId = shift1.ShiftTypeId,
                ShiftType = shift1
            });
            publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
            {
                EmployeeId = emp2.EmployeeId,
                Employee = emp2,
                WorkDate = date,
                ShiftTypeId = shift2.ShiftTypeId,
                ShiftType = shift2
            });
        }

        return publishedSchedule;
    }

    private static WorkSchedule BuildNonStandardSchedule(SchedulePeriod period, Employee emp)
    {
        var shiftA = new ShiftType
        {
            ShiftTypeId = 10,
            ShiftCode = "SHORT",
            ShiftName = "Ca ngắn",
            DurationHours = 3.5m
        };
        var shiftB = new ShiftType
        {
            ShiftTypeId = 11,
            ShiftCode = "REGULAR",
            ShiftName = "Ca chuẩn",
            DurationHours = 4m
        };

        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 20,
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            EmployeeId = emp.EmployeeId,
            Employee = emp,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 10,
            ShiftType = shiftA
        });
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            EmployeeId = emp.EmployeeId,
            Employee = emp,
            WorkDate = period.WeekStartDate.AddDays(1),
            ShiftTypeId = 11,
            ShiftType = shiftB
        });

        return publishedSchedule;
    }

    private sealed class FakeWorkScheduleRepository : IWorkScheduleRepository
    {
        public WorkSchedule? PublishedSchedule { get; set; }

        public Task<WorkSchedule?> GetByPeriodIdAsync(
            int schedulePeriodId,
            bool asNoTracking,
            bool includeAssignments = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PublishedSchedule);

        public Task<WorkSchedule?> GetPublishedByPeriodIdAsync(
            int schedulePeriodId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PublishedSchedule);

        public Task<WorkSchedule> EnsureDraftExistsAsync(
            int schedulePeriodId,
            int createdByAccountId,
            DateTime now,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PublishedSchedule ?? new WorkSchedule());

        public Task SaveAssignmentsAsync(
            int workScheduleId,
            IEnumerable<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId, AssignmentSource Source)> assignments,
            DateTime now,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishScheduleAsync(
            int workScheduleId,
            int publishedByAccountId,
            DateTime publishedAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
