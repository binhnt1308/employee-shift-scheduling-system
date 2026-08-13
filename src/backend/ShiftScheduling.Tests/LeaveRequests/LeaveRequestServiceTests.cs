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

        var item = Assert.Single(model.Requests);
        Assert.Equal(2, item.LeaveRequestId);
        Assert.Equal(LeaveRequestStatus.Approved, item.Status);
    }

    private static LeaveTestContext CreateContext(DateTime? utcNow = null)
    {
        var repository = new FakeLeaveRequestRepository();
        var employee = CreateEmployee(1, 10, "NV01", "Nguyễn An");
        repository.Employees.Add(employee);
        repository.Periods.Add(CreatePeriod());
        repository.ShiftTypes.AddRange(CreateFixedShifts());

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

    private sealed record LeaveTestContext(
        LeaveRequestService Service,
        FakeLeaveRequestRepository Repository,
        FakeNotificationRepository Notifications,
        FakeUnitOfWork UnitOfWork);
}
