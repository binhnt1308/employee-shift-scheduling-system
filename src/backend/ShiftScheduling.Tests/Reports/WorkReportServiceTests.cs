using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Reports;

public sealed class WorkReportServiceTests
{
    [Fact(DisplayName = "TC11-01 - UC11 BR18 - Tính toán tổng hợp số ca, ngày và giờ làm từ lịch chính thức đã công bố")]
    public async Task GetManagerReportOverviewAsync_WhenPublishedScheduleExists_CalculatesWorkloadCorrectly()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ lịch 33",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };
        periodRepo.Add(period);

        var emp1 = new Employee { EmployeeId = 1, EmployeeCode = "NV001", FullName = "Nguyễn Văn A", Position = EmployeePosition.Sales, EmploymentType = EmploymentType.PartTime, IsActive = true };
        var emp2 = new Employee { EmployeeId = 2, EmployeeCode = "NV002", FullName = "Trần Thị B", Position = EmployeePosition.Cashier, EmploymentType = EmploymentType.FullTime, IsActive = true };
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };

        // PT emp1: 5 ca (5 ngay) -> Dat muc du kien
        for (int d = 0; d < 5; d++)
        {
            publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
            {
                EmployeeId = 1,
                Employee = emp1,
                WorkDate = period.WeekStartDate.AddDays(d),
                ShiftTypeId = 1
            });
        }

        // FT emp2: 12 ca (6 ngay) -> Dat muc du kien
        for (int d = 0; d < 6; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            publishedSchedule.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = 2, Employee = emp2, WorkDate = date, ShiftTypeId = 1 });
            publishedSchedule.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = 2, Employee = emp2, WorkDate = date, ShiftTypeId = 2 });
        }

        workRepo.PublishedSchedule = publishedSchedule;

        var service = new WorkReportService(workRepo, periodRepo, empRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        Assert.Equal(2, report.Rows.Count);

        var row1 = report.Rows.First(r => r.EmployeeId == 1);
        Assert.Equal(5, row1.TotalShifts);
        Assert.Equal(5, row1.TotalWorkDays);
        Assert.Equal(20, row1.TotalHours);
        Assert.Equal("Đạt mức dự kiến", row1.Warning);

        var row2 = report.Rows.First(r => r.EmployeeId == 2);
        Assert.Equal(12, row2.TotalShifts);
        Assert.Equal(6, row2.TotalWorkDays);
        Assert.Equal(48, row2.TotalHours);
        Assert.Equal("Đạt mức dự kiến", row2.Warning);
    }

    [Fact(DisplayName = "TC11-02 - UC11 - Xuất CSV báo cáo có đúng header và dòng dữ liệu")]
    public async Task GenerateReportCsvAsync_ReturnsValidCsvFile()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = new SchedulePeriod { SchedulePeriodId = 1, PeriodName = "Kỳ lịch 33", WeekStartDate = new DateOnly(2026, 8, 10), WeekEndDate = new DateOnly(2026, 8, 16) };
        periodRepo.Add(period);

        var emp1 = new Employee { EmployeeId = 1, EmployeeCode = "NV001", FullName = "Nguyễn Văn A", Position = EmployeePosition.Sales, EmploymentType = EmploymentType.PartTime, IsActive = true };
        empRepo.Add(emp1);

        var publishedSchedule = new WorkSchedule { WorkScheduleId = 10, SchedulePeriodId = 1, Status = ScheduleStatus.Published };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = 1, Employee = emp1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = new WorkReportService(workRepo, periodRepo, empRepo);

        var bytes = await service.GenerateReportCsvAsync(1);
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("STT,Mã NV,Họ tên,Vị trí,Hình thức,Số ca,Số ngày,Tổng giờ,Cảnh báo", text);
        Assert.Contains("NV001", text);
        Assert.Contains("Nguyễn Văn A", text);
    }

    private sealed class FakeWorkScheduleRepository : IWorkScheduleRepository
    {
        public WorkSchedule? PublishedSchedule { get; set; }

        public Task<WorkSchedule?> GetByPeriodIdAsync(int schedulePeriodId, bool asNoTracking, bool includeAssignments = true, CancellationToken cancellationToken = default) => Task.FromResult(PublishedSchedule);
        public Task<WorkSchedule?> GetPublishedByPeriodIdAsync(int schedulePeriodId, CancellationToken cancellationToken = default) => Task.FromResult(PublishedSchedule);
        public Task<WorkSchedule> EnsureDraftExistsAsync(int schedulePeriodId, int createdByAccountId, DateTime now, CancellationToken cancellationToken = default) => Task.FromResult(PublishedSchedule ?? new WorkSchedule());
        public Task SaveAssignmentsAsync(int workScheduleId, IEnumerable<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId, AssignmentSource Source)> assignments, DateTime now, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishScheduleAsync(int workScheduleId, int publishedByAccountId, DateTime publishedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
