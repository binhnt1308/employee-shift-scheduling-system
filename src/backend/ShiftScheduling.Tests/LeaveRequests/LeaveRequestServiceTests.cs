using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.LeaveRequests.Services;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.LeaveRequests;

public sealed class LeaveRequestServiceTests
{
    [Fact(DisplayName = "TC09-01 - UC09 - Nhân viên gửi đơn hợp lệ -> Pending")]
    public async Task SubmitAsync_WhenValid_CreatesPendingRequest()
    {
        var context = CreateContext();
        var model = CreateValidModel();

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        var request = Assert.Single(context.Repository.Requests);
        Assert.Equal(LeaveRequestStatus.Pending, request.Status);
        Assert.Equal(1, request.EmployeeId);
        Assert.Equal("Có việc gia đình", request.Reason);
        Assert.Equal(1, context.UnitOfWork.SaveCount);
    }

    [Fact(DisplayName = "TC09-02 - UC09 - Ngày không thuộc kỳ lịch -> từ chối")]
    public async Task SubmitAsync_WhenDateOutsidePeriod_ReturnsInvalidDate()
    {
        var context = CreateContext();
        var model = CreateValidModel();
        model.LeaveDate = new DateOnly(2026, 8, 18);

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestInvalidDate, result.ErrorCode);
        Assert.Empty(context.Repository.Requests);
    }

    [Fact(DisplayName = "TC09-03 - UC09 - Trùng đơn còn hiệu lực cùng ngày/ca -> từ chối")]
    public async Task SubmitAsync_WhenEffectiveDuplicateExists_ReturnsDuplicateError()
    {
        var context = CreateContext();
        context.Repository.Requests.Add(CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1));

        var result = await context.Service.SubmitAsync(CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestDuplicate, result.ErrorCode);
        Assert.Single(context.Repository.Requests);
    }

    [Fact(DisplayName = "TC09-04 - UC09 - Ca đã kết thúc -> từ chối gửi đơn")]
    public async Task SubmitAsync_WhenShiftAlreadyEnded_ReturnsShiftEnded()
    {
        var context = CreateContext(new DateTime(2026, 8, 10, 6, 0, 0, DateTimeKind.Utc));
        var model = CreateValidModel();
        model.LeaveDate = new DateOnly(2026, 8, 10);
        model.ShiftTypeId = 1;

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestShiftEnded, result.ErrorCode);
        Assert.Empty(context.Repository.Requests);
    }

    [Fact(DisplayName = "TC09-05 - BR01 - Nhân viên không xem được đơn của người khác")]
    public async Task GetDetailsAsync_WhenEmployeeDoesNotOwnRequest_ReturnsNull()
    {
        var context = CreateContext();
        var other = CreateEmployee(2, 20, "NV02", "Trần Bình");
        context.Repository.Employees.Add(other);
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 2);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var details = await context.Service.GetDetailsAsync(1, 10, false);

        Assert.Null(details);
    }

    [Fact(DisplayName = "TC09-06 - UC09 BR06 BR16 - Quản lý duyệt -> Approved và thông báo")]
    public async Task ReviewAsync_WhenPendingAndApproved_UpdatesStateAndCreatesNotification()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.ReviewAsync(1, true, "Đồng ý", 2);

        Assert.True(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);
        Assert.Equal(2, request.ApprovedByAccountId);
        Assert.Equal("Đồng ý", request.ManagerNote);
        Assert.Single(context.Notifications.Items);
        Assert.Equal(NotificationType.LeaveRequestResult, context.Notifications.Items[0].Type);
    }

    [Fact(DisplayName = "TC09-07 - UC09 BR16 - Đơn đã xử lý -> không xử lý lại")]
    public async Task ReviewAsync_WhenAlreadyReviewed_ReturnsAlreadyReviewed()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Approved, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.ReviewAsync(1, false, null, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, result.ErrorCode);
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    }

    [Fact(DisplayName = "TC09-08 - UC09 - Đơn cả ngày còn hiệu lực chặn đơn theo ca cùng ngày")]
    public async Task SubmitAsync_WhenWholeDayRequestExists_BlocksSpecificShiftRequest()
    {
        var context = CreateContext();
        var wholeDay = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1, null);
        context.Repository.Requests.Add(wholeDay);

        var result = await context.Service.SubmitAsync(CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestDuplicate, result.ErrorCode);
    }

    [Fact(DisplayName = "TC09-09 - UC09 - Quản lý thấy lịch chính thức liên quan trước khi duyệt")]
    public async Task PrepareReviewAsync_WhenPublishedAssignmentExists_ShowsRelatedSchedule()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);
        context.Repository.PublishedAssignments.Add(CreatePublishedAssignment(context, 1));

        var model = await context.Service.PrepareReviewAsync(1);

        Assert.NotNull(model);
        Assert.True(model!.Details.HasPublishedAssignment);
        Assert.Equal(LeaveRequestStatus.Pending, model.Details.Status);
    }

    [Fact(DisplayName = "TC09-10 - UC09 - Nhân viên lọc đơn của bản thân theo trạng thái")]
    public async Task GetIndexAsync_WhenEmployeeFiltersByStatus_ReturnsOnlyOwnMatchingRequests()
    {
        var context = CreateContext();
        var pending = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1);
        var approved = CreateLeaveRequest(2, LeaveRequestStatus.Approved, 1, 2);
        context.Repository.Requests.AddRange([pending, approved]);
        context.Repository.WireLeaveRequest(pending);
        context.Repository.WireLeaveRequest(approved);

        var model = await context.Service.GetIndexAsync(
            10,
            false,
            1,
            LeaveRequestStatus.Approved);

        var item = Assert.Single(model.Requests.Items);
        Assert.Equal(2, item.LeaveRequestId);
        Assert.Equal(LeaveRequestStatus.Approved, item.Status);
    }

    private static LeaveTestContext CreateContext(DateTime? utcNow = null)
    {
        var repository = new FakeLeaveRequestRepository();
        var employee = CreateEmployee(1, 10, "NV01", "Nguyễn An");
        var period = CreatePeriod();
        var shifts = CreateFixedShifts();
        repository.Employees.Add(employee);
        repository.Periods.Add(period);
        repository.ShiftTypes.AddRange(shifts);
        repository.PublishedAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 1,
            WorkScheduleId = 1,
            EmployeeId = employee.EmployeeId,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 1,
            ShiftType = shifts[0],
            WorkSchedule = new WorkSchedule
            {
                WorkScheduleId = 1,
                SchedulePeriodId = period.SchedulePeriodId,
                SchedulePeriod = period,
                Status = ScheduleStatus.Published
            }
        });

        var unitOfWork = new FakeUnitOfWork();
        var notifications = new FakeNotificationRepository();
        var clock = new FakeClock
        {
            UtcNow = utcNow ?? new DateTime(2026, 8, 8, 1, 0, 0, DateTimeKind.Utc),
            TodayInVietnam = utcNow.HasValue
                ? DateOnly.FromDateTime(utcNow.Value.AddHours(7))
                : new DateOnly(2026, 8, 8)
        };
        var service = new LeaveRequestService(
            repository,
            notifications,
            new FakeAuditLogRepository(),
            unitOfWork,
            clock,
            NullLogger<LeaveRequestService>.Instance);
        return new LeaveTestContext(service, repository, notifications, unitOfWork);
    }

    private static LeaveRequestCreateViewModel CreateValidModel()
    {
        return new LeaveRequestCreateViewModel
        {
            SchedulePeriodId = 1,
            LeaveDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 1,
            Reason = "Có việc gia đình"
        };
    }

    private static Employee CreateEmployee(int id, int accountId, string code, string name)
    {
        var account = new UserAccount
        {
            UserAccountId = accountId,
            Username = code.ToLowerInvariant(),
            Role = UserRole.Employee,
            IsLocked = false
        };
        return new Employee
        {
            EmployeeId = id,
            UserAccountId = accountId,
            UserAccount = account,
            EmployeeCode = code,
            FullName = name,
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };
    }

    private static SchedulePeriod CreatePeriod()
    {
        return new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Tuần 10/08 - 16/08",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };
    }

    private static ShiftType[] CreateFixedShifts()
    {
        return
        [
            new ShiftType
            {
                ShiftTypeId = 1,
                ShiftCode = "MORNING",
                ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                DisplayOrder = 1,
                IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 2,
                ShiftCode = "AFTERNOON",
                ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(16, 0),
                DisplayOrder = 2,
                IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 3,
                ShiftCode = "EVENING",
                ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(20, 0),
                DisplayOrder = 3,
                IsActive = true
            }
        ];
    }

    [Fact(DisplayName = "DB-15 - UC09 - Quản lý duyệt đơn với RowVersion -> Đặt OriginalValue")]
    public async Task ReviewAsync_WithRowVersion_PassesOriginalValueToRepository()
    {
        var context = CreateContext();
        var originalBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var staleRowVersion = Convert.ToBase64String(originalBytes);
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1);
        request.RowVersion = [8, 7, 6, 5, 4, 3, 2, 1];
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.ReviewAsync(
            leaveRequestId: 1,
            approve: true,
            managerNote: "OK",
            managerAccountId: 99,
            rowVersion: staleRowVersion);

        Assert.True(result.Succeeded);
        Assert.Equal(originalBytes, context.Repository.TargetOriginalRowVersion);
    }

    private static LeaveRequest CreateLeaveRequest(
        int id,
        LeaveRequestStatus status,
        int employeeId,
        int? shiftTypeId = 1)
    {
        return new LeaveRequest
        {
            LeaveRequestId = id,
            EmployeeId = employeeId,
            SchedulePeriodId = 1,
            LeaveDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shiftTypeId,
            Reason = "Có việc gia đình",
            Status = status,
            SubmittedAt = new DateTime(2026, 8, 8, 1, 0, 0, DateTimeKind.Utc)
        };
    }

    private static ShiftAssignment CreatePublishedAssignment(LeaveTestContext context, int employeeId)
    {
        return new ShiftAssignment
        {
            ShiftAssignmentId = 1,
            WorkScheduleId = 1,
            EmployeeId = employeeId,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 1,
            ShiftType = context.Repository.ShiftTypes[0],
            WorkSchedule = new WorkSchedule
            {
                WorkScheduleId = 1,
                SchedulePeriodId = 1,
                SchedulePeriod = context.Repository.Periods[0],
                Status = ScheduleStatus.Published
            }
        };
    }

    [Fact(DisplayName = "LR-01 - Period chưa có Published schedule -> không xuất hiện cho Employee")]
    public async Task PrepareCreateAsync_WhenPeriodNotPublished_DoesNotAppearInDropdown()
    {
        var context = CreateContext();
        context.Repository.PublishedAssignments.Clear();

        var model = await context.Service.PrepareCreateAsync(10);

        Assert.NotNull(model);
        Assert.Empty(model!.Periods);
    }

    [Fact(DisplayName = "LR-02 - Period có Published schedule nhưng Employee không có assignment -> không xuất hiện")]
    public async Task PrepareCreateAsync_WhenPeriodPublishedButEmployeeHasNoAssignment_DoesNotAppearInDropdown()
    {
        var context = CreateContext();
        context.Repository.PublishedAssignments.Clear();
        var otherEmployee = CreateEmployee(2, 20, "NV02", "Trần Bình");
        context.Repository.Employees.Add(otherEmployee);
        context.Repository.PublishedAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 2,
            WorkScheduleId = 1,
            EmployeeId = 2,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 1,
            ShiftType = context.Repository.ShiftTypes[0],
            WorkSchedule = new WorkSchedule
            {
                WorkScheduleId = 1,
                SchedulePeriodId = 1,
                SchedulePeriod = context.Repository.Periods[0],
                Status = ScheduleStatus.Published
            }
        });

        var model = await context.Service.PrepareCreateAsync(10);

        Assert.NotNull(model);
        Assert.DoesNotContain(model!.Periods, p => p.SchedulePeriodId == 1);
    }

    [Fact(DisplayName = "LR-03 - Period Published và Employee có assignment -> xuất hiện")]
    public async Task PrepareCreateAsync_WhenPeriodPublishedAndEmployeeHasAssignment_AppearsInDropdown()
    {
        var context = CreateContext();

        var model = await context.Service.PrepareCreateAsync(10);

        Assert.NotNull(model);
        var periodOption = Assert.Single(model!.Periods);
        Assert.Equal(1, periodOption.SchedulePeriodId);
    }

    [Fact(DisplayName = "LR-04 - Ngày không có assignment -> không được submit leave")]
    public async Task SubmitAsync_WhenDateHasNoAssignment_ReturnsInvalidShiftAssignment()
    {
        var context = CreateContext();
        var model = CreateValidModel();
        model.LeaveDate = new DateOnly(2026, 8, 12);

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
    }

    [Fact(DisplayName = "LR-05 - Ngày có assignment -> hợp lệ")]
    public async Task SubmitAsync_WhenDateHasAssignment_CreatesPendingRequest()
    {
        var context = CreateContext();
        var model = CreateValidModel();

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        var req = Assert.Single(context.Repository.Requests);
        Assert.Equal(LeaveRequestStatus.Pending, req.Status);
    }

    [Fact(DisplayName = "LR-06 - Submit ShiftType mà Employee không được assign -> reject")]
    public async Task SubmitAsync_WhenShiftTypeNotAssignedToEmployee_ReturnsInvalidShiftAssignment()
    {
        var context = CreateContext();
        var model = CreateValidModel();
        model.ShiftTypeId = 2;

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
    }

    [Fact(DisplayName = "LR-07 - Submit đúng ShiftType được assign -> hợp lệ")]
    public async Task SubmitAsync_WhenShiftTypeAssignedToEmployee_CreatesPendingRequest()
    {
        var context = CreateContext();
        var model = CreateValidModel();
        model.ShiftTypeId = 1;

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Pending, Assert.Single(context.Repository.Requests).Status);
    }

    [Fact(DisplayName = "LR-08 - Submit Cả ngày khi ngày có >=1 assignment -> hợp lệ")]
    public async Task SubmitAsync_WhenWholeDayAndDateHasAssignment_CreatesPendingRequest()
    {
        var context = CreateContext();
        var model = CreateValidModel();
        model.ShiftTypeId = null;

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        var req = Assert.Single(context.Repository.Requests);
        Assert.Equal(LeaveRequestStatus.Pending, req.Status);
        Assert.Null(req.ShiftTypeId);
    }

    [Fact(DisplayName = "LR-09 - Submit Cả ngày khi ngày không có assignment -> reject")]
    public async Task SubmitAsync_WhenWholeDayAndDateHasNoAssignment_ReturnsInvalidShiftAssignment()
    {
        var context = CreateContext();
        var model = CreateValidModel();
        model.LeaveDate = new DateOnly(2026, 8, 12);
        model.ShiftTypeId = null;

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
    }

    [Fact(DisplayName = "LR-10 - Employee không thể giả SchedulePeriodId/WorkDate/ShiftTypeId ngoài assignment của mình")]
    public async Task SubmitAsync_WhenFakedPeriodOrDateOrShift_RejectsRequest()
    {
        var context = CreateContext();
        var fakeModel = new LeaveRequestCreateViewModel
        {
            SchedulePeriodId = 999,
            LeaveDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 1,
            Reason = "Faked request"
        };

        var result = await context.Service.SubmitAsync(fakeModel, 10);

        Assert.False(result.Succeeded);
        Assert.Empty(context.Repository.Requests);
    }

    [Fact(DisplayName = "LR-11 - Owner + Pending -> Cancel thành công")]
    public async Task CancelAsync_WhenOwnerAndPending_CancelsRequest()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.CancelAsync(1, 10);

        Assert.True(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Cancelled, request.Status);
        Assert.Equal(1, context.UnitOfWork.SaveCount);
    }

    [Fact(DisplayName = "LR-12 - Employee khác cố Cancel -> reject")]
    public async Task CancelAsync_WhenDifferentEmployeeAttemptsCancel_ReturnsForbidden()
    {
        var context = CreateContext();
        var other = CreateEmployee(2, 20, "NV02", "Trần Bình");
        context.Repository.Employees.Add(other);
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Pending, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.CancelAsync(1, 20);

        Assert.False(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Pending, request.Status);
    }

    [Fact(DisplayName = "LR-13 - Approved -> Employee không Cancel được")]
    public async Task CancelAsync_WhenApproved_ReturnsCannotCancel()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Approved, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.CancelAsync(1, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, result.ErrorCode);
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    }

    [Fact(DisplayName = "LR-14 - Rejected -> Employee không Cancel được")]
    public async Task CancelAsync_WhenRejected_ReturnsCannotCancel()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Rejected, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.CancelAsync(1, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, result.ErrorCode);
        Assert.Equal(LeaveRequestStatus.Rejected, request.Status);
    }

    [Fact(DisplayName = "LR-15 - Cancelled -> không Cancel lần hai")]
    public async Task CancelAsync_WhenAlreadyCancelled_CannotCancelAgain()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Cancelled, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.CancelAsync(1, 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, result.ErrorCode);
        Assert.Equal(LeaveRequestStatus.Cancelled, request.Status);
    }

    [Fact(DisplayName = "LR-16 - Manager không Approve LeaveRequest đã Cancelled")]
    public async Task ReviewAsync_WhenCancelled_ManagerCannotApprove()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Cancelled, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.ReviewAsync(1, true, "Approve", 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, result.ErrorCode);
        Assert.Equal(LeaveRequestStatus.Cancelled, request.Status);
    }

    [Fact(DisplayName = "LR-17 - Manager không Reject LeaveRequest đã Cancelled")]
    public async Task ReviewAsync_WhenCancelled_ManagerCannotReject()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Cancelled, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var result = await context.Service.ReviewAsync(1, false, "Reject", 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, result.ErrorCode);
        Assert.Equal(LeaveRequestStatus.Cancelled, request.Status);
    }

    [Fact(DisplayName = "LN-01 - Employee Submit LeaveRequest hợp lệ -> tạo exactly one Manager notification")]
    public async Task SubmitAsync_WhenValid_CreatesExactlyOneManagerNotification()
    {
        var context = CreateContext();
        context.Notifications.ManagerAccountIds.Add(99);
        var model = CreateValidModel();

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        Assert.Single(context.Notifications.Items);
    }

    [Fact(DisplayName = "LN-02 - Notification đúng Manager recipient, RelatedObjectId = LeaveRequestId, type = LeaveRequestSubmitted")]
    public async Task SubmitAsync_WhenValid_NotificationHasCorrectRecipientAndTypeAndId()
    {
        var context = CreateContext();
        context.Notifications.ManagerAccountIds.Add(99);
        var model = CreateValidModel();

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(context.Notifications.Items);
        var request = Assert.Single(context.Repository.Requests);
        Assert.Equal(99, notification.ReceiverAccountId);
        Assert.Equal(request.LeaveRequestId, notification.RelatedObjectId);
        Assert.Equal(NotificationType.LeaveRequestSubmitted, notification.Type);
        Assert.Equal("Đơn xin nghỉ mới", notification.Title);
        Assert.Equal("NV01 - Nguyễn An vừa gửi đơn xin nghỉ ca sáng ngày 10/08/2026.", notification.Content);
    }

    [Fact(DisplayName = "LN-03 - Submit invalid/fail -> không tạo Manager notification")]
    public async Task SubmitAsync_WhenInvalid_DoesNotCreateManagerNotification()
    {
        var context = CreateContext();
        context.Notifications.ManagerAccountIds.Add(99);
        var model = CreateValidModel();
        model.LeaveDate = new DateOnly(2026, 8, 18); // Outside period

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.False(result.Succeeded);
        Assert.Empty(context.Notifications.Items);
    }

    [Fact(DisplayName = "LN-04 - Employee không nhận notification LeaveRequestSubmitted của chính mình")]
    public async Task SubmitAsync_WhenEmployeeIsAlsoInManagerIds_DoesNotNotifySelf()
    {
        var context = CreateContext();
        context.Notifications.ManagerAccountIds.Add(10); // Employee's own user account id
        context.Notifications.ManagerAccountIds.Add(99); // Actual manager
        var model = CreateValidModel();

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(context.Notifications.Items);
        Assert.Equal(99, notification.ReceiverAccountId);
        Assert.DoesNotContain(context.Notifications.Items, n => n.ReceiverAccountId == 10);
    }

    [Fact(DisplayName = "LRUI-01 - Employee xem Approved Leave -> IsManager false, không có quyền review")]
    public async Task GetDetailsAsync_WhenEmployeeViewsApprovedLeave_HasIsManagerFalseAndNoReview()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Approved, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var details = await context.Service.GetDetailsAsync(1, 10, isManager: false);

        Assert.NotNull(details);
        Assert.False(details!.IsManager);
        Assert.False(details.CanReview);
        Assert.False(details.CanCancel);
        Assert.Equal(LeaveRequestStatus.Approved, details.Status);
    }

    [Fact(DisplayName = "LRUI-02 - Manager xem Approved Leave có Published conflict -> IsManager true, HasPublishedAssignment true")]
    public async Task GetDetailsAsync_WhenManagerViewsApprovedLeaveWithConflict_HasPublishedAssignmentTrue()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Approved, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var details = await context.Service.GetDetailsAsync(1, 99, isManager: true);

        Assert.NotNull(details);
        Assert.True(details!.IsManager);
        Assert.True(details.HasPublishedAssignment);
        Assert.False(details.CanReview);
        Assert.False(details.CanCancel);
    }

    [Fact(DisplayName = "LRUI-03 - Manager xem request Cancelled -> không có CanReview và không thể Review")]
    public async Task GetDetailsAsync_WhenManagerViewsCancelledRequest_CannotReview()
    {
        var context = CreateContext();
        var request = CreateLeaveRequest(1, LeaveRequestStatus.Cancelled, 1);
        context.Repository.Requests.Add(request);
        context.Repository.WireLeaveRequest(request);

        var details = await context.Service.GetDetailsAsync(1, 99, isManager: true);

        Assert.NotNull(details);
        Assert.True(details!.IsManager);
        Assert.False(details.CanReview);
        Assert.False(details.CanCancel);

        var reviewResult = await context.Service.ReviewAsync(1, true, "OK", 99);
        Assert.False(reviewResult.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, reviewResult.ErrorCode);
    }

    [Fact(DisplayName = "TC-FAILSAFE - Manager notification dependency throws -> SubmitAsync vẫn trả Success và tạo request")]
    public async Task SubmitAsync_WhenManagerNotificationThrows_StillReturnsSuccessAndCreatesRequest()
    {
        var context = CreateContext();
        context.Notifications.ThrowOnGetActiveManagers = true;
        var model = CreateValidModel();

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        var request = Assert.Single(context.Repository.Requests);
        Assert.Equal(LeaveRequestStatus.Pending, request.Status);
    }

    [Fact(DisplayName = "TC-EXPIRED-SHIFT - Ca đã qua trong quá khứ hoặc đã kết thúc hôm nay -> không hiển thị trên form")]
    public async Task PrepareCreateAsync_WhenAssignmentEndedInPastOrToday_FiltersOutExpiredAssignments()
    {
        // Thời điểm kiểm thử: 13:00 giờ Việt Nam (06:00 UTC) ngày 10/08/2026.
        // Ca 1 (08:00-12:00) đã kết thúc trong ngày.
        // Ca 2 (12:00-16:00) vẫn đang diễn ra.
        var context = CreateContext(new DateTime(2026, 8, 10, 6, 0, 0, DateTimeKind.Utc));
        context.Repository.PublishedAssignments.Clear();

        var period = context.Repository.Periods[0];
        var shifts = context.Repository.ShiftTypes;

        // 1. Phân công ngày 09/08/2026 đã qua nên phải bị loại.
        context.Repository.PublishedAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 1,
            WorkScheduleId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 9),
            ShiftTypeId = shifts[0].ShiftTypeId,
            ShiftType = shifts[0],
            WorkSchedule = new WorkSchedule { SchedulePeriodId = period.SchedulePeriodId, SchedulePeriod = period, Status = ScheduleStatus.Published }
        });

        // 2. Ca hôm nay đã kết thúc lúc 12:00 trong khi hiện tại là 13:00 nên phải bị loại.
        context.Repository.PublishedAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 2,
            WorkScheduleId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shifts[0].ShiftTypeId,
            ShiftType = shifts[0],
            WorkSchedule = new WorkSchedule { SchedulePeriodId = period.SchedulePeriodId, SchedulePeriod = period, Status = ScheduleStatus.Published }
        });

        // 3. Ca hôm nay chưa kết thúc (16:00) tại thời điểm 13:00 nên phải được giữ lại.
        context.Repository.PublishedAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 3,
            WorkScheduleId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = shifts[1].ShiftTypeId,
            ShiftType = shifts[1],
            WorkSchedule = new WorkSchedule { SchedulePeriodId = period.SchedulePeriodId, SchedulePeriod = period, Status = ScheduleStatus.Published }
        });

        var model = await context.Service.PrepareCreateAsync(10);

        Assert.NotNull(model);
        var assignment = Assert.Single(model!.AvailableAssignments);
        Assert.Equal(new DateOnly(2026, 8, 10), assignment.WorkDate);
        Assert.Equal(shifts[1].ShiftTypeId, assignment.ShiftTypeId);
    }

    [Fact(DisplayName = "TC-VERSIONED-SHIFT - ShiftType versioned và IsActive = false -> vẫn cho phép gửi Leave Request")]
    public async Task SubmitAsync_WhenShiftTypeIsVersionedAndInactive_StillAllowsSubmittingLeaveRequest()
    {
        var context = CreateContext();
        var period = context.Repository.Periods[0];

        // Tạo ca thuộc phiên bản lịch sử với IsActive = false để kiểm tra dữ liệu cũ vẫn được đọc đúng.
        var versionedShift = new ShiftType
        {
            ShiftTypeId = 7,
            ShiftCode = "MORNING_V1",
            ShiftName = "Ca sáng cũ",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DisplayOrder = 1,
            IsActive = false
        };
        context.Repository.ShiftTypes.Add(versionedShift);
        // Gắn ca lịch sử vào một phân công của lịch đã công bố.
        context.Repository.PublishedAssignments.Clear();
        context.Repository.PublishedAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 10,
            WorkScheduleId = 1,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 7,
            ShiftType = versionedShift,
            WorkSchedule = new WorkSchedule
            {
                WorkScheduleId = 1,
                SchedulePeriodId = period.SchedulePeriodId,
                SchedulePeriod = period,
                Status = ScheduleStatus.Published
            }
        });

        var model = new LeaveRequestCreateViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            LeaveDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 7,
            Reason = "Nghỉ việc gia đình"
        };

        var result = await context.Service.SubmitAsync(model, 10);

        Assert.True(result.Succeeded);
        var request = Assert.Single(context.Repository.Requests);
        Assert.Equal(7, request.ShiftTypeId);
        Assert.Equal(LeaveRequestStatus.Pending, request.Status);
    }

    [Fact(DisplayName = "TC-DEFAULT-PERIOD - Có cả kỳ bao phủ Today và kỳ tương lai -> mặc định ưu tiên kỳ bao phủ Today")]
    public async Task PrepareCreateAsync_WhenMultipleEligiblePeriods_DefaultsToPeriodCoveringToday()
    {
        // Ngày hiện tại cố định trong kiểm thử là 10/08/2026.
        var context = CreateContext(new DateTime(2026, 8, 10, 1, 0, 0, DateTimeKind.Utc));
        var period1 = context.Repository.Periods[0]; // 2026-08-10 to 2026-08-16 (covers today)

        var period2 = new SchedulePeriod
        {
            SchedulePeriodId = 2,
            PeriodName = "Tuần 17/08 - 23/08",
            WeekStartDate = new DateOnly(2026, 8, 17),
            WeekEndDate = new DateOnly(2026, 8, 23)
        };
        context.Repository.Periods.Add(period2);

        var shifts = context.Repository.ShiftTypes;
        // Kỳ lịch 2 có một phân công đã công bố cho nhân viên.
        context.Repository.PublishedAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 20,
            WorkScheduleId = 2,
            EmployeeId = 1,
            WorkDate = new DateOnly(2026, 8, 18),
            ShiftTypeId = shifts[0].ShiftTypeId,
            ShiftType = shifts[0],
            WorkSchedule = new WorkSchedule
            {
                WorkScheduleId = 2,
                SchedulePeriodId = period2.SchedulePeriodId,
                SchedulePeriod = period2,
                Status = ScheduleStatus.Published
            }
        });

        var model = await context.Service.PrepareCreateAsync(10);

        Assert.NotNull(model);
        Assert.Equal(2, model!.Periods.Count);
        Assert.Equal(1, model.SchedulePeriodId); // Defaults to Period 1 (covers today), not Period 2 (future)
    }

    private sealed record LeaveTestContext(
        LeaveRequestService Service,
        FakeLeaveRequestRepository Repository,
        FakeNotificationRepository Notifications,
        FakeUnitOfWork UnitOfWork);
}
