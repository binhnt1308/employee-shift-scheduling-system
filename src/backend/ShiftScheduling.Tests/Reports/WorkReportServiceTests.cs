using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.Reports.Controllers;
using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.WorkSchedules.Repositories;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.Reports;

public sealed class WorkReportServiceTests
{
    private static WorkReportService CreateService(
        IWorkScheduleRepository workRepo,
        ISchedulePeriodRepository periodRepo,
        IEmployeeRepository empRepo,
        FakeLeaveRequestRepository? leaveRepo = null,
        FakeAuditLogRepository? auditRepo = null,
        FakeUnitOfWork? uow = null,
        FakeClock? clock = null)
    {
        return new WorkReportService(
            workRepo,
            periodRepo,
            empRepo,
            leaveRepo ?? new FakeLeaveRequestRepository(),
            auditRepo ?? new FakeAuditLogRepository(),
            uow ?? new FakeUnitOfWork(),
            clock ?? new FakeClock(new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact(DisplayName = "TC11-01 - UC11 BR18 - Tính toán tổng hợp số ca, ngày và giờ làm từ lịch chính thức đã công bố")]
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

        var service = CreateService(workRepo, periodRepo, empRepo);

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

        var service = CreateService(workRepo, periodRepo, empRepo);

        var bytes = await service.GenerateReportCsvAsync(1);
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains(
            "STT,Mã NV,Họ tên,Vị trí,Hình thức,Số ca,Số ngày,Tổng giờ,Cảnh báo / Mức dự kiến",
            text);
        Assert.Contains("NV001", text);
        Assert.Contains("Nguyễn Văn A", text);
    }

    [Fact(DisplayName = "TC-REPORT-CONSISTENCY-01 - Manager and Employee report calculate identical TotalHours for same employee")]
    public async Task ReportOverview_ManagerAndEmployee_HaveIdenticalTotalHours()
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

        var service = CreateService(workRepo, periodRepo, empRepo);

        var managerReport = await service.GetManagerReportOverviewAsync(1);
        var employeeReport = await service.GetEmployeeOwnReportAsync(accountId: emp1.UserAccountId ?? 1, schedulePeriodId: 1);

        Assert.NotNull(managerReport);
        Assert.NotNull(employeeReport);
        Assert.NotNull(employeeReport.Summary);

        var managerRow = managerReport.Rows.First(r => r.EmployeeId == emp1.EmployeeId);
        Assert.Equal(managerRow.TotalHours, employeeReport.Summary.TotalHours);
    }

    [Fact(DisplayName = "TC-REPORT-CONSISTENCY-02 - Manager and Employee report calculate identical ShiftCount for same employee")]
    public async Task ReportOverview_ManagerAndEmployee_HaveIdenticalShiftCount()
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

        var service = CreateService(workRepo, periodRepo, empRepo);

        var managerReport = await service.GetManagerReportOverviewAsync(1);
        var employeeReport = await service.GetEmployeeOwnReportAsync(accountId: emp1.UserAccountId ?? 1, schedulePeriodId: 1);

        Assert.NotNull(managerReport);
        Assert.NotNull(employeeReport);
        Assert.NotNull(employeeReport.Summary);

        var managerRow = managerReport.Rows.First(r => r.EmployeeId == emp1.EmployeeId);
        Assert.Equal(managerRow.TotalShifts, employeeReport.Summary.TotalShifts);
    }

    [Fact(DisplayName = "TC-REPORT-CONSISTENCY-03 - Manager and Employee report produce identical WorkloadStatus warning for same employee")]
    public async Task ReportOverview_ManagerAndEmployee_HaveIdenticalWorkloadStatus()
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

        var service = CreateService(workRepo, periodRepo, empRepo);

        var managerReport = await service.GetManagerReportOverviewAsync(1);
        var employeeReport = await service.GetEmployeeOwnReportAsync(accountId: emp1.UserAccountId ?? 1, schedulePeriodId: 1);

        Assert.NotNull(managerReport);
        Assert.NotNull(employeeReport);
        Assert.NotNull(employeeReport.Summary);

        var managerRow = managerReport.Rows.First(r => r.EmployeeId == emp1.EmployeeId);
        Assert.Equal(managerRow.Warning, employeeReport.Summary.Warning);
        Assert.Equal(managerRow.IsTargetMet, employeeReport.Summary.IsTargetMet);
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

        var service = CreateService(workRepo, periodRepo, empRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var row = Assert.Single(report.Rows);
        Assert.Equal(2, row.TotalShifts);
        Assert.Equal(2, row.TotalWorkDays);
        Assert.Equal(7.5m, row.TotalHours);
    }

    // =========================================================================
    // NC-07 TEST SUITE
    // =========================================================================

    [Fact(DisplayName = "NC07-01 - Ca có đơn nghỉ Approved: Tự động tính 0 giờ, không tính vào số ca/số ngày")]
    public async Task GetManagerReportOverviewAsync_WithApprovedLeaveForSpecificShift_CalculatesZeroHoursAndDoesNotCountShift()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var (shift1, shift2) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };

        // Day 1: shift1 (has approved leave -> 0h)
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });

        // Day 2: shift2 (no leave -> 4h)
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 102,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate.AddDays(1),
            ShiftTypeId = shift2.ShiftTypeId,
            ShiftType = shift2
        });
        workRepo.PublishedSchedule = publishedSchedule;

        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            Status = LeaveRequestStatus.Approved
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var row = Assert.Single(report.Rows);
        // Only 1 working shift (Day 2), Day 1 is 0h so not counted
        Assert.Equal(1, row.TotalShifts);
        Assert.Equal(1, row.TotalWorkDays);
        Assert.Equal(4m, row.TotalHours);

        var detail1 = report.Details.FirstOrDefault(d => d.ShiftAssignmentId == 101);
        Assert.NotNull(detail1);
        Assert.True(detail1.HasApprovedLeave);
        Assert.False(detail1.IsManagerAdjusted);
        Assert.Equal(0m, detail1.EffectiveWorkedHours);
        Assert.Equal("Nghỉ đã duyệt — tự động 0 giờ", detail1.HourSourceText);

        var detail2 = report.Details.FirstOrDefault(d => d.ShiftAssignmentId == 102);
        Assert.NotNull(detail2);
        Assert.False(detail2.HasApprovedLeave);
        Assert.Equal(4m, detail2.EffectiveWorkedHours);
        Assert.Equal("Theo lịch", detail2.HourSourceText);
    }

    [Fact(DisplayName = "NC07-02 - Nghỉ cả ngày (ShiftTypeId = null): Tất cả các ca trong ngày tự động tính 0 giờ")]
    public async Task GetManagerReportOverviewAsync_WithFullDayApprovedLeave_CalculatesZeroHoursForEntireDay()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var (shift1, shift2) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };

        // Day 1 has 2 shifts for emp1
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 102,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift2.ShiftTypeId,
            ShiftType = shift2
        });
        workRepo.PublishedSchedule = publishedSchedule;

        // Full day leave
        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate,
            ShiftTypeId = null,
            Status = LeaveRequestStatus.Approved
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var row = Assert.Single(report.Rows);
        Assert.Equal(0, row.TotalShifts);
        Assert.Equal(0, row.TotalWorkDays);
        Assert.Equal(0m, row.TotalHours);

        Assert.All(report.Details, d =>
        {
            Assert.True(d.HasApprovedLeave);
            Assert.Equal(0m, d.EffectiveWorkedHours);
        });
    }

    [Fact(DisplayName = "NC07-03 - Đơn nghỉ Pending hoặc Rejected: Không tính 0 giờ, vẫn tính theo kế hoạch")]
    public async Task GetManagerReportOverviewAsync_WithPendingOrRejectedLeave_UsesPlannedHours()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var (shift1, shift2) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 102,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate.AddDays(1),
            ShiftTypeId = shift2.ShiftTypeId,
            ShiftType = shift2
        });
        workRepo.PublishedSchedule = publishedSchedule;

        // Pending and Rejected leaves
        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            Status = LeaveRequestStatus.Pending
        });
        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 2,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate.AddDays(1),
            ShiftTypeId = shift2.ShiftTypeId,
            Status = LeaveRequestStatus.Rejected
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var row = Assert.Single(report.Rows);
        Assert.Equal(2, row.TotalShifts);
        Assert.Equal(2, row.TotalWorkDays);
        Assert.Equal(8m, row.TotalHours);
    }

    [Fact(DisplayName = "NC07-04 - Nghỉ Approved của nhân viên khác: Không ảnh hưởng phân công của nhân viên hiện tại")]
    public async Task GetManagerReportOverviewAsync_WithApprovedLeaveForDifferentEmployeeOrDate_DoesNotAffectOtherAssignments()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, emp2) = CreateTwoEmployees();
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        var (shift1, _) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        // Approved leave for emp2, not emp1
        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp2.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            Status = LeaveRequestStatus.Approved
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var row1 = report.Rows.First(r => r.EmployeeId == emp1.EmployeeId);
        Assert.Equal(1, row1.TotalShifts);
        Assert.Equal(4m, row1.TotalHours);
    }

    [Fact(DisplayName = "NC07-05 - Quản lý điều chỉnh giảm giờ có lý do: Thành công, lưu DB và ghi AuditLog ADJUST_WORKDAY_HOURS")]
    public async Task AdjustWorkdayHoursAsync_WithValidActualHoursAndReason_UpdatesAssignmentAndLogsAudit()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 2.5m,
            WorkdayAdjustmentReason = "Về sớm 1.5h có phép",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Equal(2.5m, assignment.ActualWorkedHours);
        Assert.Equal("Về sớm 1.5h có phép", assignment.WorkdayAdjustmentReason);
        Assert.Equal(1, uow.SaveCount);

        var audit = Assert.Single(auditRepo.Items);
        Assert.Equal("ADJUST_WORKDAY_HOURS", audit.Action);
        Assert.Equal("ShiftAssignment", audit.EntityName);
        Assert.Equal(99, audit.ActorAccountId);
        Assert.Contains("2.5", audit.NewValues);
        Assert.Contains("WorkdayAdjustmentReason", audit.NewValues);
    }

    [Fact(DisplayName = "NC07-06 - Quản lý điều chỉnh ca có nghỉ phép Approved thành > 0 giờ: Thành công khi có lý do")]
    public async Task AdjustWorkdayHoursAsync_OnShiftWithApprovedLeave_CanAdjustToPositiveHoursWithReason()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            Status = LeaveRequestStatus.Approved
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 3m,
            WorkdayAdjustmentReason = "Nhân viên đồng ý vào làm hỗ trợ 3h",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Equal(3m, assignment.ActualWorkedHours);
        Assert.Equal("Nhân viên đồng ý vào làm hỗ trợ 3h", assignment.WorkdayAdjustmentReason);

        // Verify report now reflects 3 hours and counts as 1 shift
        var report = await service.GetManagerReportOverviewAsync(1);
        Assert.NotNull(report);
        var row = Assert.Single(report.Rows);
        Assert.Equal(1, row.TotalShifts);
        Assert.Equal(3m, row.TotalHours);
    }

    [Fact(DisplayName = "NC07-07 - Reset về mặc định khi có nghỉ phép Approved: Revert về 0 giờ, xóa override")]
    public async Task AdjustWorkdayHoursAsync_ResetToSystem_WhenApprovedLeaveExists_RevertsToZero()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            ActualWorkedHours = 3m,
            WorkdayAdjustmentReason = "Từng sửa 3h"
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            Status = LeaveRequestStatus.Approved
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            IsReset = true
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Null(assignment.ActualWorkedHours);
        Assert.Null(assignment.WorkdayAdjustmentReason);
        Assert.Equal(1, uow.SaveCount);

        var audit = Assert.Single(auditRepo.Items);
        Assert.Contains("\"ActualWorkedHours\":null", audit.NewValues);

        // Overview check: should be 0h because approved leave exists
        var report = await service.GetManagerReportOverviewAsync(1);
        Assert.NotNull(report);
        var detail = Assert.Single(report.Details);
        Assert.Equal(0m, detail.EffectiveWorkedHours);
        Assert.True(detail.HasApprovedLeave);
        Assert.False(detail.IsManagerAdjusted);
    }

    [Fact(DisplayName = "NC07-08 - Reset về mặc định khi không có nghỉ phép: Revert về giờ theo ca DurationHours")]
    public async Task AdjustWorkdayHoursAsync_ResetToSystem_WhenNoApprovedLeave_RevertsToPlannedHours()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            ActualWorkedHours = 1m,
            WorkdayAdjustmentReason = "Sửa 1h"
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            IsReset = true
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Null(assignment.ActualWorkedHours);
        Assert.Null(assignment.WorkdayAdjustmentReason);

        var report = await service.GetManagerReportOverviewAsync(1);
        Assert.NotNull(report);
        var detail = Assert.Single(report.Details);
        Assert.Equal(4m, detail.EffectiveWorkedHours);
        Assert.False(detail.IsManagerAdjusted);
        Assert.Equal("Theo lịch", detail.HourSourceText);
    }

    [Fact(DisplayName = "NC07-09 - Reset ca chưa từng bị điều chỉnh (ActualWorkedHours null): No-op, không lưu DB, không ghi audit")]
    public async Task AdjustWorkdayHoursAsync_ResetToSystem_WhenAlreadyNull_IsNoOp()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            ActualWorkedHours = null,
            WorkdayAdjustmentReason = null
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            IsReset = true
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "NC07-10 - Nhập giờ bằng đúng số giờ hệ thống: Tự động normalize thành null/null")]
    public async Task AdjustWorkdayHoursAsync_WhenInputEqualsSystemHours_NormalizesToNullNull()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            ActualWorkedHours = 2m,
            WorkdayAdjustmentReason = "Đã từng sửa"
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        // Planned is 4m. Input 4m with reason or not -> normalizes to reset
        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 4m,
            WorkdayAdjustmentReason = "Nhập lại 4h",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Null(assignment.ActualWorkedHours);
        Assert.Null(assignment.WorkdayAdjustmentReason);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact(DisplayName = "NC07-11 - Ca chưa kết thúc: Chặn điều chỉnh và trả lỗi nghiệp vụ")]
    public async Task AdjustWorkdayHoursAsync_WhenShiftNotEndedYet_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();

        // Work date 2026-08-10, shift ends 11:30. Current time is 2026-08-10 10:00 (before end)
        var clock = new FakeClock(new DateTime(2026, 8, 10, 3, 0, 0, DateTimeKind.Utc)); // 10:00 VN

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var shift1 = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca Sáng",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30),
            DurationHours = 4m
        };
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 2m,
            WorkdayAdjustmentReason = "Sửa sớm",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("kết thúc", result.ErrorMessage);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact(DisplayName = "NC07-12 - Ca đêm vắt qua ngày (Overnight): Tính đúng thời điểm kết thúc ở ngày hôm sau")]
    public async Task AdjustWorkdayHoursAsync_WhenOvernightShiftNotEndedYet_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();

        // Shift starts 22:00 (Day 10), ends 06:00 (Day 11). Current time 05:00 VN on Day 11 (UTC 22:00 on Day 10)
        var clock = new FakeClock(new DateTime(2026, 8, 10, 22, 0, 0, DateTimeKind.Utc)); // 05:00 VN 11/08

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var nightShift = new ShiftType
        {
            ShiftTypeId = 3,
            ShiftCode = "NIGHT",
            ShiftName = "Ca Đêm",
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0), // End <= Start -> overnight
            DurationHours = 8m
        };
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = nightShift.ShiftTypeId,
            ShiftType = nightShift
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo, auditRepo, uow, clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 6m,
            WorkdayAdjustmentReason = "Ca đêm",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("kết thúc", result.ErrorMessage);
    }

    [Fact(DisplayName = "NC07-13 - Lịch chưa công bố (Draft): Chặn điều chỉnh")]
    public async Task AdjustWorkdayHoursAsync_WhenScheduleNotPublished_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var (shift1, _) = CreateStandardFourHourShifts();
        var draftSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Draft // Draft
        };
        draftSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = draftSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 2m,
            WorkdayAdjustmentReason = "Thử điều chỉnh Draft",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("đã công bố", result.ErrorMessage);
    }

    [Fact(DisplayName = "NC07-14 - Giờ thực tế âm: Trả lỗi validation")]
    public async Task AdjustWorkdayHoursAsync_WhenHoursNegative_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = -0.5m,
            WorkdayAdjustmentReason = "Giờ âm",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("phải từ 0 đến", result.ErrorMessage);
    }

    [Fact(DisplayName = "NC07-15 - Giờ thực tế vượt quá 24 giờ: Trả lỗi validation, không SaveChanges, không AuditLog")]
    public async Task AdjustWorkdayHoursAsync_WhenHoursExceed24_ReturnsFailureWithoutSavingOrAuditing()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);
        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);
        var (shift1, _) = CreateStandardFourHourShifts(); // Duration 4h
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, auditRepo: auditRepo, uow: uow, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 24.01m, // Exceeds 24h
            WorkdayAdjustmentReason = "Làm việc quá 24h",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Equal("HoursOutOfRange", result.ErrorCode);
        Assert.Contains("phải từ 0 đến 24 giờ", result.ErrorMessage);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "NC07-16 - Thiếu lý do điều chỉnh khi giờ khác hệ thống: Trả lỗi validation")]
    public async Task AdjustWorkdayHoursAsync_WhenDifferentFromSystemAndReasonEmpty_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 2m,
            WorkdayAdjustmentReason = "   ", // Empty/Whitespace
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("lý do", result.ErrorMessage);
    }

    [Fact(DisplayName = "NC07-17 - Không tìm thấy kỳ lịch hoặc lịch làm việc: Trả lỗi")]
    public async Task AdjustWorkdayHoursAsync_WhenSchedulePeriodNotFound_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var service = CreateService(workRepo, periodRepo, empRepo);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 999,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 2m,
            WorkdayAdjustmentReason = "Không có kỳ",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("Không tìm thấy lịch làm việc", result.ErrorMessage);
    }

    [Fact(DisplayName = "NC07-18 - Không tìm thấy phân công ca làm việc: Trả lỗi")]
    public async Task AdjustWorkdayHoursAsync_WhenAssignmentNotFound_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 9999, // Non-existent assignment
            ActualWorkedHours = 2m,
            WorkdayAdjustmentReason = "Non-existent",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("Không tìm thấy phân công ca", result.ErrorMessage);
    }

    [Fact(DisplayName = "NC07-19 - Đổi ca thành công: Người nhận được tính giờ, người chuyển không bị tính giờ")]
    public async Task ShiftTransfer_RecipientGetsHoursAndSenderGetsZeroHours()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, emp2) = CreateTwoEmployees();
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        var (shift1, _) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };

        // Shift was originally assigned, then transferred to emp2
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp2.EmployeeId, // Recipient
            Employee = emp2,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            Source = AssignmentSource.ShiftTransfer
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var rowEmp1 = report.Rows.FirstOrDefault(r => r.EmployeeId == emp1.EmployeeId);
        var rowEmp2 = report.Rows.FirstOrDefault(r => r.EmployeeId == emp2.EmployeeId);

        // emp1 has 0 shifts and 0h
        Assert.NotNull(rowEmp1);
        Assert.Equal(0, rowEmp1.TotalShifts);
        Assert.Equal(0m, rowEmp1.TotalHours);

        // emp2 has 1 shift and 4h
        Assert.NotNull(rowEmp2);
        Assert.Equal(1, rowEmp2.TotalShifts);
        Assert.Equal(4m, rowEmp2.TotalHours);
    }

    [Fact(DisplayName = "NC07-20 - Tính nhất quán: Báo cáo Quản lý và Báo cáo Cá nhân đồng nhất khi có nghỉ phép và điều chỉnh giờ")]
    public async Task ReportConsistency_WhenAdjustedAndLeaveExist_ManagerAndEmployeeReportsMatch()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, emp2) = CreateTwoEmployees();
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        var (shift1, shift2) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };

        // Shift 1: Normal (4h)
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });

        // Shift 2: Approved Leave (0h)
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 102,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate.AddDays(1),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate.AddDays(1),
            ShiftTypeId = shift1.ShiftTypeId,
            Status = LeaveRequestStatus.Approved
        });

        // Shift 3: Manager adjusted to 2.5h
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 103,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate.AddDays(2),
            ShiftTypeId = shift2.ShiftTypeId,
            ShiftType = shift2,
            ActualWorkedHours = 2.5m,
            WorkdayAdjustmentReason = "Về sớm"
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var managerReport = await service.GetManagerReportOverviewAsync(1);
        var empReport = await service.GetEmployeeOwnReportAsync(emp1.UserAccountId ?? 1, 1);

        Assert.NotNull(managerReport);
        Assert.NotNull(empReport);
        Assert.NotNull(empReport.Summary);

        var managerRow = managerReport.Rows.First(r => r.EmployeeId == emp1.EmployeeId);
        Assert.Equal(managerRow.TotalShifts, empReport.Summary.TotalShifts);
        Assert.Equal(managerRow.TotalWorkDays, empReport.Summary.TotalWorkDays);
        Assert.Equal(managerRow.TotalHours, empReport.Summary.TotalHours);
        Assert.Equal(managerRow.Warning, empReport.Summary.Warning);
        Assert.Equal(managerRow.IsTargetMet, empReport.Summary.IsTargetMet);

        // Total hours should be 4 + 0 + 2.5 = 6.5h, TotalShifts = 2 (shift 2 is 0h so excluded)
        Assert.Equal(6.5m, managerRow.TotalHours);
        Assert.Equal(2, managerRow.TotalShifts);
        Assert.Equal(2, managerRow.TotalWorkDays);
    }

    [Fact(DisplayName = "NC07-21 - Xuất CSV khớp chính xác số giờ đã điều chỉnh và đơn nghỉ phép")]
    public async Task GenerateReportCsvAsync_WhenAdjustedAndLeaveExist_MatchesManagerReportValues()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            ActualWorkedHours = 2.5m,
            WorkdayAdjustmentReason = "Điều chỉnh CSV"
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var bytes = await service.GenerateReportCsvAsync(1);
        var csv = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("NV001", csv);
        Assert.Contains("2.5", csv);
    }

    [Fact(DisplayName = "NC07-22 - Security: Action AdjustWorkdayHours trên ReportController có Authorize Manager, HttpPost, ValidateAntiForgeryToken")]
    public void ReportController_AdjustWorkdayHours_HasRequiredSecurityAttributes()
    {
        var method = typeof(ReportController).GetMethod(
            nameof(ReportController.AdjustWorkdayHours),
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(RoleNames.Manager, authAttr.Roles);

        var postAttr = method.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(postAttr);

        var antiForgeryAttr = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();
        Assert.NotNull(antiForgeryAttr);
    }

    [Fact(DisplayName = "NC07-23 - Chi tiết ngày công: Bộ lọc nhân viên và vị trí lọc chính xác Details")]
    public async Task GetManagerReportOverviewAsync_FiltersDetailsCorrectly()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, emp2) = CreateTwoEmployees(); // emp1 Sales, emp2 Cashier
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        var (shift1, _) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 102,
            WorkScheduleId = 10,
            EmployeeId = emp2.EmployeeId,
            Employee = emp2,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo);

        var filteredByEmp = await service.GetManagerReportOverviewAsync(1, employeeIdFilter: emp1.EmployeeId);
        Assert.NotNull(filteredByEmp);
        Assert.Single(filteredByEmp.Details);
        Assert.Equal(emp1.EmployeeId, filteredByEmp.Details[0].EmployeeId);

        var filteredByPos = await service.GetManagerReportOverviewAsync(1, positionFilter: EmployeePosition.Cashier);
        Assert.NotNull(filteredByPos);
        Assert.Single(filteredByPos.Details);
        Assert.Equal(emp2.EmployeeId, filteredByPos.Details[0].EmployeeId);
    }

    [Fact(DisplayName = "NC07-24 - CanAdjust trong Details: Đúng khi ca đã kết thúc trên lịch Published")]
    public async Task WorkdayDetailItemViewModel_CanAdjust_EvaluatesCorrectly()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        // Clock is 12:00 VN on Day 10
        var clock = new FakeClock(new DateTime(2026, 8, 10, 5, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var shiftEnded = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca Sáng",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30), // Ended before 12:00
            DurationHours = 4m
        };
        var shiftNotEnded = new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca Chiều",
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0), // Ends after 12:00
            DurationHours = 4m
        };

        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shiftEnded.ShiftTypeId,
            ShiftType = shiftEnded
        });
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 102,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shiftNotEnded.ShiftTypeId,
            ShiftType = shiftNotEnded
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, clock: clock);

        var report = await service.GetManagerReportOverviewAsync(1);
        Assert.NotNull(report);

        var detailEnded = report.Details.First(d => d.ShiftAssignmentId == 101);
        var detailNotEnded = report.Details.First(d => d.ShiftAssignmentId == 102);

        Assert.True(detailEnded.CanAdjust);
        Assert.False(detailNotEnded.CanAdjust);
    }

    [Fact(DisplayName = "NC07-25 - Audit Log ghi nhận đúng ID tài khoản người thực hiện điều chỉnh")]
    public async Task AdjustWorkdayHoursAsync_AuditLogContainsCorrectActorAccountId()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, auditRepo: auditRepo, uow: uow, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 3m,
            WorkdayAdjustmentReason = "Điều chỉnh bởi Quản lý ID 105",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(105, request);

        Assert.True(result.Succeeded);
        var log = Assert.Single(auditRepo.Items);
        Assert.Equal(105, log.ActorAccountId);
        Assert.Equal("101", log.EntityId);
    }

    [Fact(DisplayName = "NC07-26 - Nghỉ khác ca: Đơn nghỉ Approved cùng nhân viên, cùng ngày nhưng khác ca không làm phân công ca hiện tại thành 0 giờ")]
    public async Task GetManagerReportOverviewAsync_WithApprovedLeaveForDifferentShiftOnSameDate_DoesNotAffectCurrentShift()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);

        var (shift1, shift2) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };

        // Shift 1 is assigned
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        // Approved leave for emp1 on the same date, but for shift2 (different shift)
        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId,
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate,
            ShiftTypeId = shift2.ShiftTypeId, // Different shift!
            Status = LeaveRequestStatus.Approved
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var row = Assert.Single(report.Rows);
        Assert.Equal(1, row.TotalShifts);
        Assert.Equal(1, row.TotalWorkDays);
        Assert.Equal(4m, row.TotalHours);

        var detail = Assert.Single(report.Details);
        Assert.False(detail.HasApprovedLeave);
        Assert.Equal(4m, detail.EffectiveWorkedHours);
        Assert.Equal("Theo lịch", detail.HourSourceText);
    }

    [Fact(DisplayName = "NC07-27 - Nghỉ của người gửi cũ sau đổi ca: Đơn nghỉ Approved của người gửi không làm phân công của người nhận thành 0 giờ")]
    public async Task GetManagerReportOverviewAsync_WhenShiftTransferred_SenderApprovedLeaveDoesNotAffectRecipientHours()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var leaveRepo = new FakeLeaveRequestRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);

        var (emp1, emp2) = CreateTwoEmployees(); // emp1 = sender, emp2 = recipient
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        var (shift1, _) = CreateStandardFourHourShifts();
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };

        // Shift was assigned to emp2 (recipient) after transfer
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp2.EmployeeId, // Receiver
            Employee = emp2,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            Source = AssignmentSource.ShiftTransfer
        });
        workRepo.PublishedSchedule = publishedSchedule;

        // Sender (emp1) has an approved leave for this date and shift
        leaveRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = emp1.EmployeeId, // Sender!
            SchedulePeriodId = 1,
            LeaveDate = period.WeekStartDate,
            ShiftTypeId = shift1.ShiftTypeId,
            Status = LeaveRequestStatus.Approved
        });

        var service = CreateService(workRepo, periodRepo, empRepo, leaveRepo);

        var report = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(report);
        var rowRecipient = report.Rows.First(r => r.EmployeeId == emp2.EmployeeId);
        Assert.Equal(1, rowRecipient.TotalShifts);
        Assert.Equal(1, rowRecipient.TotalWorkDays);
        Assert.Equal(4m, rowRecipient.TotalHours);

        var detail = Assert.Single(report.Details);
        Assert.Equal(emp2.EmployeeId, detail.EmployeeId);
        Assert.False(detail.HasApprovedLeave);
        Assert.Equal(4m, detail.EffectiveWorkedHours);
    }

    [Fact(DisplayName = "NC07-28 - Reason quá 500 ký tự: Bị validation reject, không SaveChanges, không ghi AuditLog")]
    public async Task AdjustWorkdayHoursAsync_WhenReasonExceeds500Chars_ReturnsFailureWithoutSavingOrAuditing()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, auditRepo: auditRepo, uow: uow, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 2m, // Different from system calculated hours (4m)
            WorkdayAdjustmentReason = new string('A', 501), // Length > 500
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Contains("500", result.ErrorMessage);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "NC07-29 - Không thay đổi identity fields: Sau điều chỉnh hợp lệ, EmployeeId, WorkDate, ShiftTypeId, WorkScheduleId giữ nguyên")]
    public async Task AdjustWorkdayHoursAsync_WhenValidAdjustment_PreservesIdentityFields()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, auditRepo: auditRepo, uow: uow, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 2.5m,
            WorkdayAdjustmentReason = "Điều chỉnh hợp lệ",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        // Verify identity fields are untouched
        Assert.Equal(101, assignment.ShiftAssignmentId);
        Assert.Equal(emp1.EmployeeId, assignment.EmployeeId);
        Assert.Equal(new DateOnly(2026, 8, 10), assignment.WorkDate);
        Assert.Equal(shift1.ShiftTypeId, assignment.ShiftTypeId);
        Assert.Equal(10, assignment.WorkScheduleId);

        // Verify only workday adjustment fields changed
        Assert.Equal(2.5m, assignment.ActualWorkedHours);
        Assert.Equal("Điều chỉnh hợp lệ", assignment.WorkdayAdjustmentReason);
        Assert.NotNull(assignment.UpdatedAt);

        // Verify AuditLog
        var audit = Assert.Single(auditRepo.Items);
        Assert.Equal("ADJUST_WORKDAY_HOURS", audit.Action);
        Assert.Equal("ShiftAssignment", audit.EntityName);
        Assert.Equal("101", audit.EntityId);
        Assert.Equal(99, audit.ActorAccountId);
    }

    [Fact(DisplayName = "NC07-30 - Cho phép giờ thực tế vượt quá DurationHours đến 24h khi có reason: Thành công, lưu AuditLog và SaveChanges")]
    public async Task AdjustWorkdayHoursAsync_WhenHoursExceedDurationUpTo24WithReason_SucceedsAndSavesAuditLog()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);
        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);
        var (shift1, _) = CreateStandardFourHourShifts(); // Duration 4h
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, auditRepo: auditRepo, uow: uow, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 5m, // Exceeds 4h DurationHours
            WorkdayAdjustmentReason = "Tăng ca thêm 1 tiếng",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Equal(5m, assignment.ActualWorkedHours);
        Assert.Equal(5m, assignment.ActualWorkedHours ?? assignment.ShiftType.DurationHours);
        Assert.Equal(1, uow.SaveCount);
        var audit = Assert.Single(auditRepo.Items);
        Assert.Equal("ADJUST_WORKDAY_HOURS", audit.Action);
        Assert.Equal("ShiftAssignment", audit.EntityName);
        Assert.Equal("101", audit.EntityId);
        Assert.Equal(99, audit.ActorAccountId);
    }

    [Fact(DisplayName = "NC07-31 - Vượt DurationHours nhưng thiếu reason: Reject, không SaveChanges, không AuditLog")]
    public async Task AdjustWorkdayHoursAsync_WhenHoursExceedDurationWithEmptyReason_ReturnsFailureWithoutSavingOrAuditing()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var auditRepo = new FakeAuditLogRepository();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, auditRepo: auditRepo, uow: uow, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 5m,
            WorkdayAdjustmentReason = "   ", // Blank reason
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Equal("ReasonRequired", result.ErrorCode);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "NC07-32 - Cho phép điều chỉnh đúng 24 giờ khi có reason: Thành công")]
    public async Task AdjustWorkdayHoursAsync_WhenHoursExactly24WithReason_Succeeds()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        };
        publishedSchedule.ShiftAssignments.Add(assignment);
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = 24m,
            WorkdayAdjustmentReason = "Trực liên tục 24h thay ca đặc biệt",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.True(result.Succeeded);
        Assert.Equal(24m, assignment.ActualWorkedHours);
    }

    [Fact(DisplayName = "NC07-33 - Giờ thực tế âm -0.01: Trả lỗi validation HoursOutOfRange")]
    public async Task AdjustWorkdayHoursAsync_WhenHoursNegative001_ReturnsFailure()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var clock = new FakeClock(new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc));

        var period = CreatePeriod(1, "Kỳ 1");
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
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo, clock: clock);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = 1,
            ShiftAssignmentId = 101,
            ActualWorkedHours = -0.01m,
            WorkdayAdjustmentReason = "Giờ âm nhỏ",
            IsReset = false
        };

        var result = await service.AdjustWorkdayHoursAsync(99, request);

        Assert.False(result.Succeeded);
        Assert.Equal("HoursOutOfRange", result.ErrorCode);
        Assert.Contains("phải từ 0 đến 24 giờ", result.ErrorMessage);
    }

    [Fact(DisplayName = "NC07-34 - Báo cáo tổng hợp sử dụng giờ thực tế khi vượt kế hoạch: TotalHours tăng theo giờ điều chỉnh")]
    public async Task GetManagerReportOverviewAsync_WhenAssignmentAdjustedToExceedDurationHours_UsesAdjustedHoursInTotal()
    {
        var workRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();

        var period = CreatePeriod(1, "Kỳ 1");
        periodRepo.Add(period);
        var (emp1, _) = CreateTwoEmployees();
        empRepo.Add(emp1);
        var (shift1, _) = CreateStandardFourHourShifts(); // Duration 4h

        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 10,
            SchedulePeriodId = 1,
            Status = ScheduleStatus.Published
        };
        publishedSchedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 101,
            WorkScheduleId = 10,
            EmployeeId = emp1.EmployeeId,
            Employee = emp1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shift1.ShiftTypeId,
            ShiftType = shift1,
            ActualWorkedHours = 5m, // Exceeds 4h
            WorkdayAdjustmentReason = "Tăng ca 1 tiếng"
        });
        workRepo.PublishedSchedule = publishedSchedule;

        var service = CreateService(workRepo, periodRepo, empRepo);

        var overview = await service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(overview);
        var row = Assert.Single(overview.Rows);
        Assert.Equal(5m, row.TotalHours);
        Assert.Equal(1, row.TotalShifts);
        Assert.Equal(1, row.TotalWorkDays);

        var detail = Assert.Single(overview.Details);
        Assert.Equal(4m, detail.PlannedHours);
        Assert.Equal(5m, detail.EffectiveWorkedHours);
        Assert.True(detail.IsManagerAdjusted);
        Assert.Equal("Tăng ca 1 tiếng", detail.AdjustmentReason);
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
            UserAccountId = 1,
            EmployeeCode = "NV001",
            FullName = "Nguyễn Văn A",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            IsActive = true
        };
        var emp2 = new Employee
        {
            EmployeeId = 2,
            UserAccountId = 2,
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
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DurationHours = 4m
        };
        var shift2 = new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca Chiều",
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0),
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
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(11, 30),
            DurationHours = 3.5m
        };
        var shiftB = new ShiftType
        {
            ShiftTypeId = 11,
            ShiftCode = "REGULAR",
            ShiftName = "Ca chuẩn",
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0),
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

        public Task<HashSet<int>> GetPublishedPeriodIdsAsync(
            IEnumerable<int> periodIds,
            CancellationToken cancellationToken = default)
        {
            var ids = periodIds.ToHashSet();
            var set = PublishedSchedule is not null
                && PublishedSchedule.Status == ScheduleStatus.Published
                && ids.Contains(PublishedSchedule.SchedulePeriodId)
                ? new HashSet<int> { PublishedSchedule.SchedulePeriodId }
                : new HashSet<int>();
            return Task.FromResult(set);
        }

        public Task<WorkSchedule?> GetLatestPublishedScheduleAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PublishedSchedule);

        public Task<bool> HasFuturePublishedAssignmentAsync(
            int employeeId,
            DateOnly fromDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
