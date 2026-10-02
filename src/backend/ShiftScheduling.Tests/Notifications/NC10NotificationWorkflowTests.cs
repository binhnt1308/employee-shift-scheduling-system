using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShiftScheduling.Common.Email;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.LeaveRequests.Services;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Features.ShiftTransfers.Services;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.Notifications;

public sealed class NC10NotificationWorkflowTests
{
    // ============================================================
    // N10-01 & N10-E01: công bố lịch, tạo thông báo nội bộ và gửi email.
    // ============================================================

    [Fact(DisplayName = "N10-01 - PublishScheduleAsync thành công -> tạo thông báo nội bộ SchedulePublished đúng recipient")]
    public async Task N10_01_PublishSchedule_Success_CreatesInternalSchedulePublishedForAssignedEmployees()
    {
        var fixture = CreateWorkScheduleFixture();
        var period = fixture.Period;

        var result = await fixture.ScheduleService.PublishScheduleAsync(period.SchedulePeriodId, accountId: 2);

        Assert.True(result.Succeeded);
        // Có 2 nhân viên được phân công: Emp1 (AccountId 11) và Emp2 (AccountId 12)
        var notifications = fixture.NotificationRepo.Items;
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n =>
        {
            Assert.Equal(NotificationType.SchedulePublished, n.Type);
            Assert.Equal(period.SchedulePeriodId, n.RelatedObjectId);
            Assert.Contains("công bố", n.Title, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(period.PeriodName, n.Content);
            Assert.False(n.IsRead);
        });

        Assert.Contains(notifications, n => n.ReceiverAccountId == 11);
        Assert.Contains(notifications, n => n.ReceiverAccountId == 12);
    }

    [Fact(DisplayName = "N10-E01 - PublishScheduleAsync thành công -> gửi email deduplicated đúng recipients")]
    public async Task N10_E01_PublishSchedule_Success_SendsDeduplicatedEmailToAssignedEmployees()
    {
        var fixture = CreateWorkScheduleFixture();
        var period = fixture.Period;

        // Emp1 có 2 ca phân công trong kỳ nhưng chỉ được nhận 1 email
        var result = await fixture.ScheduleService.PublishScheduleAsync(period.SchedulePeriodId, accountId: 2);

        Assert.True(result.Succeeded);
        var emails = fixture.EmailSender.SentEmails;
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, e => e.ToEmail == "emp1@example.com");
        Assert.Contains(emails, e => e.ToEmail == "emp2@example.com");
        Assert.All(emails, e =>
        {
            Assert.Equal("Lịch làm việc đã được công bố", e.Subject);
            Assert.Contains(period.PeriodName, e.Body);
        });
    }

    // ============================================================
    // N10-02, N10-05 & N10-E02: duyệt chuyển ca, cập nhật lịch và gửi thông báo.
    // ============================================================

    [Fact(DisplayName = "N10-02 - Shift Transfer Approved -> Sender và Receiver nhận duy nhất 1 notification ScheduleChanged")]
    public async Task N10_02_ShiftTransfer_Approved_SenderAndReceiverReceiveScheduleChanged()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);

        var result = await context.Service.ReviewAsync(
            request.ShiftTransferRequestId,
            approve: true,
            managerNote: "Đồng ý đổi ca",
            managerAccountId: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalStatus.Approved, request.ApprovalStatus);

        // Mỗi người nhận đúng 1 notification (tổng cộng 2)
        Assert.Equal(2, context.Notifications.Items.Count);
        Assert.All(context.Notifications.Items, n =>
        {
            Assert.Equal(NotificationType.ScheduleChanged, n.Type);
            Assert.False(n.IsRead);
        });

        Assert.Contains(context.Notifications.Items, n => n.ReceiverAccountId == context.Sender.UserAccountId!.Value);
        Assert.Contains(context.Notifications.Items, n => n.ReceiverAccountId == context.Receiver.UserAccountId!.Value);
    }

    [Fact(DisplayName = "N10-05 - Shift Transfer Approved -> Nội dung thể hiện request approved + lịch công bố đã cập nhật")]
    public async Task N10_05_ShiftTransfer_Approved_NotificationContent_ReflectsApprovalAndScheduleUpdate()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);

        await context.Service.ReviewAsync(
            request.ShiftTransferRequestId,
            approve: true,
            managerNote: "Đồng ý",
            managerAccountId: 2);

        var senderNotice = context.Notifications.Items.Single(n => n.ReceiverAccountId == context.Sender.UserAccountId!.Value);
        Assert.Equal("Lịch làm việc có thay đổi", senderNotice.Title);
        Assert.Contains("duyệt", senderNotice.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lịch làm việc chính thức", senderNotice.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(context.Period.SchedulePeriodId, senderNotice.RelatedObjectId);
    }

    [Fact(DisplayName = "N10-E02 - Shift Transfer Approved -> Sender và Receiver nhận đúng 1 email mỗi người")]
    public async Task N10_E02_ShiftTransfer_Approved_SenderAndReceiverReceiveExactlyOneEmailEach()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);

        await context.Service.ReviewAsync(
            request.ShiftTransferRequestId,
            approve: true,
            managerNote: "Đồng ý",
            managerAccountId: 2);

        var emails = context.EmailSender.SentEmails;
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, e => e.ToEmail == context.Sender.Email);
        Assert.Contains(emails, e => e.ToEmail == context.Receiver.Email);
        Assert.All(emails, e =>
        {
            Assert.Contains("Lịch làm việc đã thay đổi", e.Subject);
            Assert.Contains("phê duyệt", e.Body);
        });
    }

    // ============================================================
    // N10-06 & N10-E06: từ chối chuyển ca và thông báo kết quả.
    // ============================================================

    [Fact(DisplayName = "N10-06 - Shift Transfer Rejected -> Sender và Receiver nhận ShiftTransferResult thể hiện bị từ chối")]
    public async Task N10_06_ShiftTransfer_Rejected_SenderAndReceiverReceiveShiftTransferResult()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);

        var result = await context.Service.ReviewAsync(
            request.ShiftTransferRequestId,
            approve: false,
            managerNote: "Không đủ nhân sự thay thế",
            managerAccountId: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalStatus.Rejected, request.ApprovalStatus);

        Assert.Equal(2, context.Notifications.Items.Count);
        Assert.All(context.Notifications.Items, n =>
        {
            Assert.Equal(NotificationType.ShiftTransferResult, n.Type);
            Assert.Equal("Kết quả yêu cầu đổi ca", n.Title);
            Assert.Contains("từ chối", n.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(request.ShiftTransferRequestId, n.RelatedObjectId);
        });

        Assert.Contains(context.Notifications.Items, n => n.ReceiverAccountId == context.Sender.UserAccountId!.Value);
        Assert.Contains(context.Notifications.Items, n => n.ReceiverAccountId == context.Receiver.UserAccountId!.Value);
    }

    [Fact(DisplayName = "N10-E06 - Shift Transfer Rejected -> Sender và Receiver nhận email từ chối")]
    public async Task N10_E06_ShiftTransfer_Rejected_SenderAndReceiverReceiveRejectionEmail()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);

        await context.Service.ReviewAsync(
            request.ShiftTransferRequestId,
            approve: false,
            managerNote: "Không đủ người",
            managerAccountId: 2);

        var emails = context.EmailSender.SentEmails;
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, e => e.ToEmail == context.Sender.Email);
        Assert.Contains(emails, e => e.ToEmail == context.Receiver.Email);
        Assert.All(emails, e =>
        {
            Assert.Equal("Kết quả yêu cầu đổi ca", e.Subject);
            Assert.Contains("từ chối", e.Body);
            Assert.Contains("Không đủ người", e.Body);
        });
    }

    // ============================================================
    // N10-03 & N10-E03: duyệt đơn nghỉ và gửi thông báo.
    // ============================================================

    [Fact(DisplayName = "N10-03 - Leave Approved -> LeaveRequestResult đúng recipient")]
    public async Task N10_03_LeaveRequest_Approved_CreatesLeaveRequestResultForEmployee()
    {
        var fixture = CreateLeaveFixture();
        var request = fixture.CreateLeaveRequest(LeaveRequestStatus.Pending);

        var result = await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: true, "Đồng ý nghỉ", 2);

        Assert.True(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);

        var notice = Assert.Single(fixture.Notifications.Items);
        Assert.Equal(NotificationType.LeaveRequestResult, notice.Type);
        Assert.Equal(request.Employee.UserAccountId!.Value, notice.ReceiverAccountId);
        Assert.Equal(request.LeaveRequestId, notice.RelatedObjectId);
        Assert.Equal("Kết quả đơn xin nghỉ", notice.Title);
        Assert.Contains("được duyệt", notice.Content);
    }

    [Fact(DisplayName = "N10-E03 - Leave Approved -> Gửi email phê duyệt tới đúng Employee.Email")]
    public async Task N10_E03_LeaveRequest_Approved_SendsEmailToEmployee()
    {
        var fixture = CreateLeaveFixture();
        var request = fixture.CreateLeaveRequest(LeaveRequestStatus.Pending);

        await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: true, "Đồng ý", 2);

        var email = Assert.Single(fixture.EmailSender.SentEmails);
        Assert.Equal(request.Employee.Email, email.ToEmail);
        Assert.Equal("Kết quả đơn xin nghỉ", email.Subject);
        Assert.Contains("phê duyệt", email.Body);
        Assert.Contains(request.LeaveDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture), email.Body);
    }

    // ============================================================
    // N10-04 & N10-E04: từ chối đơn nghỉ và gửi thông báo.
    // ============================================================

    [Fact(DisplayName = "N10-04 - Leave Rejected -> LeaveRequestResult đúng recipient và nội dung từ chối")]
    public async Task N10_04_LeaveRequest_Rejected_CreatesLeaveRequestResultForEmployee()
    {
        var fixture = CreateLeaveFixture();
        var request = fixture.CreateLeaveRequest(LeaveRequestStatus.Pending);

        var result = await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: false, "Ca thiếu người", 2);

        Assert.True(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Rejected, request.Status);

        var notice = Assert.Single(fixture.Notifications.Items);
        Assert.Equal(NotificationType.LeaveRequestResult, notice.Type);
        Assert.Equal(request.Employee.UserAccountId!.Value, notice.ReceiverAccountId);
        Assert.Contains("bị từ chối", notice.Content);
    }

    [Fact(DisplayName = "N10-E04 - Leave Rejected -> Gửi email từ chối tới đúng Employee.Email")]
    public async Task N10_E04_LeaveRequest_Rejected_SendsEmailToEmployee()
    {
        var fixture = CreateLeaveFixture();
        var request = fixture.CreateLeaveRequest(LeaveRequestStatus.Pending);

        await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: false, "Ca thiếu người", 2);

        var email = Assert.Single(fixture.EmailSender.SentEmails);
        Assert.Equal(request.Employee.Email, email.ToEmail);
        Assert.Equal("Kết quả đơn xin nghỉ", email.Subject);
        Assert.Contains("từ chối", email.Body);
        Assert.Contains("Ca thiếu người", email.Body);
    }

    // ============================================================
    // Khả năng chịu lỗi N10-F01 & N10-F02: lỗi thông báo không được làm hỏng nghiệp vụ chính.
    // ============================================================

    [Fact(DisplayName = "N10-F01 - Employee.Email null -> Business thành công, internal notification vẫn có, không gọi send email")]
    public async Task N10_F01_WhenEmployeeEmailIsNull_BusinessSucceeds_NotificationCreated_EmailNotCalled()
    {
        var fixture = CreateLeaveFixture();
        var request = fixture.CreateLeaveRequest(LeaveRequestStatus.Pending);
        request.Employee.Email = null; // Không có email

        var result = await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: true, "OK", 2);

        Assert.True(result.Succeeded);
        Assert.Single(fixture.Notifications.Items);
        Assert.Empty(fixture.EmailSender.SentEmails);
        Assert.Equal(0, fixture.EmailSender.SendCallCount);
    }

    [Fact(DisplayName = "N10-F02 - FakeEmailSender throws -> Business state vẫn lưu thành công, internal notification tồn tại, không văng exception")]
    public async Task N10_F02_WhenFakeEmailSenderThrows_BusinessStateSaved_NotificationExists_NoExceptionEscapes()
    {
        var fixture = CreateLeaveFixture();
        var request = fixture.CreateLeaveRequest(LeaveRequestStatus.Pending);
        fixture.EmailSender.ThrowOnSend = true; // Giả lập lỗi SMTP văng exception

        var result = await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: true, "OK", 2);

        // Nghiệp vụ chính vẫn thành công bình thường
        Assert.True(result.Succeeded);
        Assert.Equal(LeaveRequestStatus.Approved, request.Status);
        Assert.Single(fixture.Notifications.Items);
    }

    // ============================================================
    // Tính lặp an toàn N10-F03: không tạo thông báo trùng khi cùng sự kiện được xử lý lại.
    // ============================================================

    [Fact(DisplayName = "N10-F03 - Thao tác đã hoàn tất gọi lại -> Guard chặn, không phát sinh email/notification duplicate")]
    public async Task N10_F03_WhenBusinessAlreadyCompleted_GuardBlocks_NoDuplicateNotificationOrEmail()
    {
        var fixture = CreateLeaveFixture();
        var request = fixture.CreateLeaveRequest(LeaveRequestStatus.Pending);

        // Lần 1: Duyệt thành công
        var firstResult = await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: true, "OK", 2);
        Assert.True(firstResult.Succeeded);
        Assert.Single(fixture.Notifications.Items);
        Assert.Single(fixture.EmailSender.SentEmails);

        // Lần 2: Gọi lại đơn đã duyệt
        var secondResult = await fixture.Service.ReviewAsync(request.LeaveRequestId, approve: true, "OK", 2);
        Assert.False(secondResult.Succeeded);
        Assert.Equal(ErrorCodes.LeaveRequestAlreadyReviewed, secondResult.ErrorCode);

        // Vẫn chỉ có 1 notification và 1 email từ lần duyệt trước
        Assert.Single(fixture.Notifications.Items);
        Assert.Single(fixture.EmailSender.SentEmails);
    }

    [Fact(DisplayName = "N10-SmtpDisabled - SmtpEmailSender với Enabled = false -> Không gửi và không throw")]
    public async Task SmtpEmailSender_WhenDisabled_DoesNotThrowAndSkips()
    {
        var options = Options.Create(new SmtpSettings { Enabled = false });
        var sender = new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            sender.SendAsync("test@example.com", "Subject", "Body"));

        Assert.Null(exception);
    }

    // ============================================================
    // Các fixture hỗ trợ dựng dữ liệu kiểm thử.
    // ============================================================

    private static WorkScheduleFixture CreateWorkScheduleFixture()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var availRepo = new FakeAvailabilityRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var workScheduleRepo = new FakeWorkScheduleRepository();
        var notificationRepo = new FakeNotificationRepository();
        var auditLogRepo = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock();
        var validationService = new FakeSchedulingValidationService();
        var orToolsService = new OrToolsSchedulingService(new SchedulingValidationService(), NullLogger<OrToolsSchedulingService>.Instance);
        var emailSender = new FakeEmailSender();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Tuần 10/08 - 16/08",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            RegistrationStatus = RegistrationStatus.Closed,
            StaffingRequirements = new List<StaffingRequirement>()
        };
        periodRepo.SchedulePeriods.Add(period);

        var morning = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca Sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DurationHours = 4,
            DisplayOrder = 1,
            IsActive = true,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false
        };
        var afternoon = new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca Chiều",
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(16, 0),
            DurationHours = 4,
            DisplayOrder = 2,
            IsActive = true,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false
        };
        var evening = new ShiftType
        {
            ShiftTypeId = 3,
            ShiftCode = "EVENING",
            ShiftName = "Ca Tối",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(20, 0),
            DurationHours = 4,
            DisplayOrder = 3,
            IsActive = true,
            MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false
        };
        shiftTypeRepo.ShiftTypes.AddRange([morning, afternoon, evening]);

        // Tạo staffing requirements cho cả 3 ca của ngày đầu tiên (đủ 3 ca)
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            StaffingRequirementId = 1,
            SchedulePeriodId = 1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 1,
            ShiftType = morning,
            RequiredSalesStaff = 1,
            RequiredCashiers = 0,
            RequiredExperiencedStaff = 0,
            MinimumLevel = EmployeeLevel.Level2
        });
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            StaffingRequirementId = 2,
            SchedulePeriodId = 1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 2,
            ShiftType = afternoon,
            RequiredSalesStaff = 1,
            RequiredCashiers = 0,
            RequiredExperiencedStaff = 0,
            MinimumLevel = EmployeeLevel.Level2
        });
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            StaffingRequirementId = 3,
            SchedulePeriodId = 1,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 3,
            ShiftType = evening,
            RequiredSalesStaff = 0,
            RequiredCashiers = 0,
            RequiredExperiencedStaff = 0,
            MinimumLevel = EmployeeLevel.Level2
        });

        var emp1 = new Employee
        {
            EmployeeId = 1,
            UserAccountId = 11,
            EmployeeCode = "NV01",
            FullName = "Nguyễn Văn Một",
            Email = "emp1@example.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };
        var emp2 = new Employee
        {
            EmployeeId = 2,
            UserAccountId = 12,
            EmployeeCode = "NV02",
            FullName = "Trần Thị Hai",
            Email = "emp2@example.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };
        employeeRepo.Employees.AddRange([emp1, emp2]);

        availRepo.Availabilities.Add(new Availability { EmployeeId = 1, SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 });
        availRepo.Availabilities.Add(new Availability { EmployeeId = 2, SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 2 });

        var schedule = new WorkSchedule
        {
            WorkScheduleId = 1,
            SchedulePeriodId = 1,
            SchedulePeriod = period,
            ScheduleName = "Lịch nháp tuần",
            Status = ScheduleStatus.Draft,
            ShiftAssignments =
            [
                new ShiftAssignment { ShiftAssignmentId = 1, WorkScheduleId = 1, EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1, ShiftType = morning, Employee = emp1 },
                new ShiftAssignment { ShiftAssignmentId = 2, WorkScheduleId = 1, EmployeeId = 2, WorkDate = period.WeekStartDate, ShiftTypeId = 2, ShiftType = afternoon, Employee = emp2 },
                // Emp1 có thêm 1 ca khác để kiểm tra deduplicate email
                new ShiftAssignment { ShiftAssignmentId = 3, WorkScheduleId = 1, EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1, ShiftType = morning, Employee = emp1 }
            ]
        };
        workScheduleRepo.Schedules.Add(schedule);

        var service = new WorkScheduleService(
            workScheduleRepo,
            periodRepo,
            shiftTypeRepo,
            employeeRepo,
            availRepo,
            leaveRepo,
            validationService,
            orToolsService,
            notificationRepo,
            auditLogRepo,
            unitOfWork,
            clock,
            NullLogger<WorkScheduleService>.Instance,
            emailSender);

        return new WorkScheduleFixture(service, period, notificationRepo, emailSender);
    }

    private sealed record WorkScheduleFixture(
        WorkScheduleService ScheduleService,
        SchedulePeriod Period,
        FakeNotificationRepository NotificationRepo,
        FakeEmailSender EmailSender);

    private static LeaveFixture CreateLeaveFixture()
    {
        var repository = new FakeLeaveRequestRepository();
        var employee = new Employee
        {
            EmployeeId = 1,
            UserAccountId = 10,
            EmployeeCode = "NV01",
            FullName = "Nguyễn An",
            Email = "nguyenan@example.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };
        repository.Employees.Add(employee);
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Tuần 10/08 - 16/08",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };
        repository.Periods.Add(period);
        var shiftType = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DisplayOrder = 1,
            IsActive = true
        };
        repository.ShiftTypes.Add(shiftType);

        var notifications = new FakeNotificationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock();
        var emailSender = new FakeEmailSender();

        var service = new LeaveRequestService(
            repository,
            notifications,
            new FakeAuditLogRepository(),
            unitOfWork,
            clock,
            NullLogger<LeaveRequestService>.Instance,
            emailSender);

        return new LeaveFixture(service, repository, notifications, emailSender, employee, period, shiftType);
    }

    private sealed class LeaveFixture
    {
        public LeaveRequestService Service { get; }
        public FakeLeaveRequestRepository Repository { get; }
        public FakeNotificationRepository Notifications { get; }
        public FakeEmailSender EmailSender { get; }
        public Employee Employee { get; }
        public SchedulePeriod Period { get; }
        public ShiftType ShiftType { get; }

        public LeaveFixture(
            LeaveRequestService service,
            FakeLeaveRequestRepository repository,
            FakeNotificationRepository notifications,
            FakeEmailSender emailSender,
            Employee employee,
            SchedulePeriod period,
            ShiftType shiftType)
        {
            Service = service;
            Repository = repository;
            Notifications = notifications;
            EmailSender = emailSender;
            Employee = employee;
            Period = period;
            ShiftType = shiftType;
        }

        public LeaveRequest CreateLeaveRequest(LeaveRequestStatus status)
        {
            var request = new LeaveRequest
            {
                LeaveRequestId = 1,
                EmployeeId = Employee.EmployeeId,
                Employee = Employee,
                SchedulePeriodId = Period.SchedulePeriodId,
                SchedulePeriod = Period,
                LeaveDate = new DateOnly(2026, 8, 10),
                ShiftTypeId = ShiftType.ShiftTypeId,
                ShiftType = ShiftType,
                Reason = "Có việc gia đình",
                Status = status,
                SubmittedAt = new DateTime(2026, 8, 8, 1, 0, 0, DateTimeKind.Utc)
            };
            Repository.Requests.Add(request);
            Repository.WireLeaveRequest(request);
            return request;
        }
    }

    private sealed class FakeSchedulingValidationService : ISchedulingValidationService
    {
        public ScheduleValidationResultViewModel ValidateSchedule(
            SchedulePeriod period,
            IReadOnlyList<Employee> activeEmployees,
            IReadOnlyList<Availability> availabilities,
            IReadOnlyList<LeaveRequest> approvedLeaves,
            IReadOnlyList<StaffingRequirement> staffingRequirements,
            IReadOnlyList<ShiftAssignment> assignments,
            IReadOnlyList<ShiftType> shiftTypes)
        {
            return new ScheduleValidationResultViewModel
            {
                IsValid = true,
                HardErrors = new List<string>(),
                Warnings = new List<string>()
            };
        }
    }
}
