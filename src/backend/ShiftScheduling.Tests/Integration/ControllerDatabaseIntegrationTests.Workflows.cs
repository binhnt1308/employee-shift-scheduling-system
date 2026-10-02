using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.LeaveRequests.Controllers;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Features.Reports.Controllers;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Features.WorkSchedules.Controllers;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Constants;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.Integration;

public sealed partial class ControllerDatabaseIntegrationTests
{
    // ============================================================
    // IT-03 – Gửi đơn nghỉ và Quản lý xử lý thông qua Controller.
    // ============================================================
    [Fact(DisplayName = "IT-03 - Luồng gửi đơn xin nghỉ và Quản lý xử lý qua LeaveRequestController cập nhật trạng thái và AuditLog trong database")]
    public async Task IT03_LeaveRequestWorkflow_SubmitAndApproveThroughController_UpdatesDatabaseStateAndAudit()
    {
        // 1. Chuẩn bị lịch đã công bố có một phân công cho nhân viên 1.
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        var weekStartDate = new DateOnly(2026, 10, 19);

        await using var setupDb = _fixture.CreateDbContext();
        var period = await SeedSchedulePeriodWith21RequirementsAsync(setupDb, weekStartDate, clock);

        var leaveDate = weekStartDate.AddDays(1); // Tuesday

        var publishedSchedule = new WorkSchedule
        {
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Published,
            CreatedByAccountId = 2,
            PublishedByAccountId = 2,
            PublishedAt = clock.UtcNow.AddDays(-1),
            CreatedAt = clock.UtcNow.AddDays(-2)
        };
        setupDb.WorkSchedules.Add(publishedSchedule);

        var assignment = new ShiftAssignment
        {
            WorkSchedule = publishedSchedule,
            EmployeeId = 1,
            WorkDate = leaveDate,
            ShiftTypeId = 1,
            Source = AssignmentSource.Manual,
            CreatedAt = clock.UtcNow.AddDays(-2)
        };
        setupDb.ShiftAssignments.Add(assignment);
        await setupDb.SaveChangesAsync();

        // Bước 1: Nhân viên gửi đơn qua LeaveRequestController
        var empController = CreateLeaveRequestController(setupDb, clock, accountId: 3, role: RoleNames.Employee);
        var createModel = new LeaveRequestCreateViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            LeaveDate = leaveDate,
            ShiftTypeId = 1,
            Reason = "Nghỉ việc gia đình có việc đột xuất"
        };

        var createResult = await empController.Create(createModel, CancellationToken.None);
        var createRedirect = Assert.IsType<RedirectToActionResult>(createResult);
        Assert.Equal(nameof(LeaveRequestController.Index), createRedirect.ActionName);

        // Đọc lại trực tiếp từ cơ sở dữ liệu để xác nhận trạng thái Chờ duyệt.
        await using var queryDb1 = _fixture.CreateDbContext();
        var pendingLeave = await queryDb1.LeaveRequests
            .FirstOrDefaultAsync(l => l.SchedulePeriodId == period.SchedulePeriodId && l.EmployeeId == 1);

        Assert.NotNull(pendingLeave);
        Assert.Equal(LeaveRequestStatus.Pending, pendingLeave.Status);
        Assert.Equal(1, pendingLeave.EmployeeId);
        Assert.Equal(period.SchedulePeriodId, pendingLeave.SchedulePeriodId);
        Assert.Equal(leaveDate, pendingLeave.LeaveDate);
        Assert.Equal(1, pendingLeave.ShiftTypeId);

        var expectedEntityId = $"{pendingLeave.EmployeeId}:{pendingLeave.LeaveDate:yyyyMMdd}:{pendingLeave.ShiftTypeId?.ToString() ?? "DAY"}";
        var submitAudit = await queryDb1.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "SUBMIT_LEAVE_REQUEST" &&
            a.EntityName == nameof(LeaveRequest) &&
            a.EntityId == expectedEntityId);
        Assert.NotNull(submitAudit);
        Assert.Equal(3, submitAudit.ActorAccountId);

        // Bước 2: Quản lý xử lý đơn qua LeaveRequestController
        await using var managerDb = _fixture.CreateDbContext();
        var managerController = CreateLeaveRequestController(managerDb, clock, accountId: 2, role: RoleNames.Manager);

        var reviewModel = new LeaveRequestReviewViewModel
        {
            LeaveRequestId = pendingLeave.LeaveRequestId,
            ManagerNote = "Từ chối do không đủ nhân sự thay thế",
            RowVersion = Convert.ToBase64String(pendingLeave.RowVersion)
        };

        var reviewResult = await managerController.Review(
            pendingLeave.LeaveRequestId, reviewModel, approve: false, CancellationToken.None);

        var reviewRedirect = Assert.IsType<RedirectToActionResult>(reviewResult);
        Assert.Equal(nameof(LeaveRequestController.Index), reviewRedirect.ActionName);

        // Đọc lại trực tiếp từ cơ sở dữ liệu để xác nhận trạng thái Từ chối và thông tin người duyệt.
        await using var queryDb2 = _fixture.CreateDbContext();
        var reviewedLeave = await queryDb2.LeaveRequests.FindAsync(pendingLeave.LeaveRequestId);

        Assert.NotNull(reviewedLeave);
        Assert.Equal(LeaveRequestStatus.Rejected, reviewedLeave.Status);
        Assert.Equal(2, reviewedLeave.ApprovedByAccountId);
        Assert.NotNull(reviewedLeave.ReviewedAt);
        Assert.Equal("Từ chối do không đủ nhân sự thay thế", reviewedLeave.ManagerNote);

        var reviewAudit = await queryDb2.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "REVIEW_LEAVE_REQUEST" &&
            a.EntityName == nameof(LeaveRequest) &&
            a.EntityId == pendingLeave.LeaveRequestId.ToString());
        Assert.NotNull(reviewAudit);
        Assert.Equal(2, reviewAudit.ActorAccountId);
    }

    // ============================================================
    // IT-04 – Phân công thủ công thông qua WorkScheduleController.
    // ============================================================
    [Fact(DisplayName = "IT-04 - Phân công thủ công qua WorkScheduleController lưu ShiftAssignment thật Source=Manual và AuditLog vào database")]
    public async Task IT04_ManualAssignment_ThroughWorkScheduleController_SavesAssignmentAndAuditToDatabase()
    {
        // 1. Chuẩn bị dữ liệu tiền điều kiện.
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        var weekStartDate = new DateOnly(2026, 10, 26);
        var workDate = weekStartDate; // Monday

        await using var setupDb = _fixture.CreateDbContext();
        var period = await SeedSchedulePeriodWith21RequirementsAsync(setupDb, weekStartDate, clock);

        // Đăng ký lịch rảnh cho Employee 1 vào ca sáng thứ 2
        setupDb.Availabilities.Add(new Availability
        {
            EmployeeId = 1,
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = workDate,
            ShiftTypeId = 1,
            PreferenceLevel = AvailabilityPreferenceLevel.Preferred,
            RegisteredAt = clock.UtcNow
        });

        // Tạo sẵn WorkSchedule ở trạng thái Draft để thao tác phân công có lịch đích.
        var draftSchedule = new WorkSchedule
        {
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Draft,
            CreatedByAccountId = 2,
            CreatedAt = clock.UtcNow
        };
        setupDb.WorkSchedules.Add(draftSchedule);
        await setupDb.SaveChangesAsync();

        // 2. Action: Quản lý gọi WorkScheduleController.AssignManual
        await using var actionDb = _fixture.CreateDbContext();
        var controller = CreateWorkScheduleController(actionDb, clock, accountId: 2, role: RoleNames.Manager);

        var assignModel = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = workDate,
            ShiftTypeId = 1,
            EmployeeId = 1
        };

        var actionResult = await controller.AssignManual(assignModel, CancellationToken.None);

        // 3. Controller phải trả JsonResult báo thao tác thành công.
        var jsonResult = Assert.IsType<JsonResult>(actionResult);
        var json = JsonSerializer.Serialize(jsonResult.Value);
        using var jsonDoc = JsonDocument.Parse(json);
        Assert.True(jsonDoc.RootElement.GetProperty("success").GetBoolean());

        // 4. Dùng DbContext mới để đọc lại dữ liệu từ cơ sở dữ liệu.
        await using var queryDb = _fixture.CreateDbContext();
        var assignment = await queryDb.ShiftAssignments
            .Include(a => a.WorkSchedule)
            .FirstOrDefaultAsync(a =>
                a.WorkSchedule.SchedulePeriodId == period.SchedulePeriodId &&
                a.EmployeeId == 1 &&
                a.WorkDate == workDate &&
                a.ShiftTypeId == 1);

        Assert.NotNull(assignment);
        Assert.Equal(AssignmentSource.Manual, assignment.Source);
        Assert.Equal(draftSchedule.WorkScheduleId, assignment.WorkScheduleId);

        // Kiểm tra AuditLog đã được lưu trong cơ sở dữ liệu.
        var audit = await queryDb.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "MANUAL_ASSIGN_EMPLOYEE" &&
            a.EntityName == nameof(ShiftAssignment) &&
            a.ActorAccountId == 2);

        Assert.NotNull(audit);
    }

    // ============================================================
    // IT-05 – NC-07 điều chỉnh giờ công thực tế thông qua ReportController.
    // ============================================================
    [Fact(DisplayName = "IT-05 - Điều chỉnh giờ công thực tế NC-07 qua ReportController cập nhật database và phản ánh vào báo cáo tổng hợp")]
    public async Task IT05_AdjustActualWorkedHours_ThroughReportController_UpdatesAssignmentAndReflectedInReport()
    {
        // 1. Chuẩn bị dữ liệu tiền điều kiện.
        var weekStartDate = new DateOnly(2026, 11, 2);
        var shiftDate = weekStartDate; // Monday

        // Clock đặt sau khi ca đã kết thúc (Ca sáng 8:00 - 12:00 kết thúc vào trưa cùng ngày)
        var clock = new SqlServerIntegrationFixture.TestClock(new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc));

        await using var setupDb = _fixture.CreateDbContext();
        var period = await SeedSchedulePeriodWith21RequirementsAsync(setupDb, weekStartDate, clock);

        // Tạo WorkSchedule ở trạng thái Published vì chỉ lịch đã công bố mới được điều chỉnh giờ công.
        var publishedSchedule = new WorkSchedule
        {
            SchedulePeriodId = period.SchedulePeriodId,
            Status = ScheduleStatus.Published,
            CreatedByAccountId = 2,
            PublishedByAccountId = 2,
            PublishedAt = clock.UtcNow.AddDays(-1),
            CreatedAt = clock.UtcNow.AddDays(-2)
        };
        setupDb.WorkSchedules.Add(publishedSchedule);

        // Thêm phân công ca Sáng 4 giờ cho nhân viên 1 làm dữ liệu đầu vào của báo cáo.
        var assignment = new ShiftAssignment
        {
            WorkSchedule = publishedSchedule,
            EmployeeId = 1,
            WorkDate = shiftDate,
            ShiftTypeId = 1, // MORNING (4h)
            Source = AssignmentSource.Manual,
            CreatedAt = clock.UtcNow.AddDays(-2)
        };
        setupDb.ShiftAssignments.Add(assignment);
        await setupDb.SaveChangesAsync();

        // 2. Action: Quản lý gọi ReportController.AdjustWorkdayHours điều chỉnh từ 4h lên 5h
        await using var actionDb = _fixture.CreateDbContext();
        var controller = CreateReportController(actionDb, clock, accountId: 2, role: RoleNames.Manager);

        var request = new AdjustWorkdayHoursRequest
        {
            SchedulePeriodId = period.SchedulePeriodId,
            ShiftAssignmentId = assignment.ShiftAssignmentId,
            ActualWorkedHours = 5m,
            WorkdayAdjustmentReason = "Nhân viên tăng ca thêm 1 tiếng",
            IsReset = false
        };

        var actionResult = await controller.AdjustWorkdayHours(request, CancellationToken.None);

        // 3. Kiểm tra phản hồi của Controller.
        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal(nameof(ReportController.Index), redirect.ActionName);

        // 4. Dùng DbContext mới để đọc lại dữ liệu và kiểm tra tác động sau thao tác.
        await using var queryDb = _fixture.CreateDbContext();
        var updatedAssignment = await queryDb.ShiftAssignments.FindAsync(assignment.ShiftAssignmentId);

        Assert.NotNull(updatedAssignment);
        Assert.Equal(5m, updatedAssignment.ActualWorkedHours);
        Assert.Equal("Nhân viên tăng ca thêm 1 tiếng", updatedAssignment.WorkdayAdjustmentReason);

        // Kiểm tra AuditLog được ghi nhận.
        var audit = await queryDb.AuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "ADJUST_WORKDAY_HOURS" &&
            a.EntityName == nameof(ShiftAssignment) &&
            a.EntityId == assignment.ShiftAssignmentId.ToString());

        Assert.NotNull(audit);
        Assert.Equal(2, audit.ActorAccountId);

        // 5. Đọc lại bằng WorkReportService thật để xác nhận dữ liệu báo cáo đã được tổng hợp đúng.
        var reportService = CreateRealWorkReportService(queryDb, clock);
        var overview = await reportService.GetManagerReportOverviewAsync(period.SchedulePeriodId);

        Assert.NotNull(overview);
        var empRow = Assert.Single(overview.Rows, r => r.EmployeeId == 1);
        Assert.Equal(5m, empRow.TotalHours); // Phản ánh 5h sau điều chỉnh, không phải 4h kế hoạch

        var detail = Assert.Single(overview.Details, d => d.ShiftAssignmentId == assignment.ShiftAssignmentId);
        Assert.Equal(4m, detail.PlannedHours);
        Assert.Equal(5m, detail.EffectiveWorkedHours);
        Assert.True(detail.IsManagerAdjusted);
        Assert.Equal("Nhân viên tăng ca thêm 1 tiếng", detail.AdjustmentReason);
    }
}
