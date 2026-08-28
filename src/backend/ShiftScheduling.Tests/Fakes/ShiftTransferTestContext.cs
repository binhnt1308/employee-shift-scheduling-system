using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.ShiftTransfers.Services;
using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.Fakes;

internal sealed class ShiftTransferTestContext
{
    private ShiftTransferTestContext()
    {
        Repository = new FakeShiftTransferRepository();
        Notifications = new FakeNotificationRepository();
        Notifications.ManagerAccountIds.Add(2);
        UnitOfWork = new FakeUnitOfWork();
        AuditLogs = new FakeAuditLogRepository();
        Clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 8, 8, 1, 0, 0, DateTimeKind.Utc),
            TodayInVietnam = new DateOnly(2026, 8, 8)
        };

        Period = CreatePeriod();
        Morning = CreateShift(1, "MORNING", "Ca sáng", 8, 12);
        Afternoon = CreateShift(2, "AFTERNOON", "Ca chiều", 12, 16);
        Evening = CreateShift(3, "EVENING", "Ca tối", 16, 20);
        Schedule = new WorkSchedule
        {
            WorkScheduleId = 1,
            SchedulePeriodId = Period.SchedulePeriodId,
            SchedulePeriod = Period,
            ScheduleName = "Lịch chính thức tuần 33",
            Status = ScheduleStatus.Published,
            PublishedAt = new DateTime(2026, 8, 7, 1, 0, 0, DateTimeKind.Utc)
        };

        Sender = CreateEmployee(1, 10, "NV01", "Nguyễn An", EmployeeLevel.Level2);
        Receiver = CreateEmployee(2, 20, "NV02", "Trần Bình", EmployeeLevel.Level2);
        Repository.Employees.AddRange([Sender, Receiver]);

        TargetAssignment = CreateAssignment(1, Sender, new DateOnly(2026, 8, 10), Morning);
        Repository.Assignments.Add(TargetAssignment);
        Repository.Availabilities.Add(CreateAvailability(Receiver, TargetAssignment));
        Repository.Requirements.Add(CreateRequirement(TargetAssignment));

        Service = new ShiftTransferService(
            Repository,
            Notifications,
            AuditLogs,
            UnitOfWork,
            Clock,
            NullLogger<ShiftTransferService>.Instance);
    }

    public FakeShiftTransferRepository Repository { get; }
    public FakeNotificationRepository Notifications { get; }
    public FakeUnitOfWork UnitOfWork { get; }
    public FakeAuditLogRepository AuditLogs { get; }
    public FakeClock Clock { get; }
    public ShiftTransferService Service { get; }
    public SchedulePeriod Period { get; }
    public WorkSchedule Schedule { get; }
    public ShiftType Morning { get; }
    public ShiftType Afternoon { get; }
    public ShiftType Evening { get; }
    public Employee Sender { get; }
    public Employee Receiver { get; }
    public ShiftAssignment TargetAssignment { get; }

    public static ShiftTransferTestContext Create()
    {
        return new ShiftTransferTestContext();
    }

    public ShiftTransferCreateViewModel CreateValidModel()
    {
        return new ShiftTransferCreateViewModel
        {
            ShiftAssignmentId = TargetAssignment.ShiftAssignmentId,
            ReceiverEmployeeId = Receiver.EmployeeId,
            Reason = "Nhờ bạn nhận giúp ca này"
        };
    }

    public ShiftTransferRequest CreateRequest(
        int id = 1,
        ReceiverStatus receiverStatus = ReceiverStatus.Pending,
        ApprovalStatus approvalStatus = ApprovalStatus.Pending)
    {
        var request = new ShiftTransferRequest
        {
            ShiftTransferRequestId = id,
            ShiftAssignmentId = TargetAssignment.ShiftAssignmentId,
            SenderEmployeeId = Sender.EmployeeId,
            ReceiverEmployeeId = Receiver.EmployeeId,
            Reason = "Nhờ bạn nhận giúp ca này",
            ReceiverStatus = receiverStatus,
            ApprovalStatus = approvalStatus,
            SubmittedAt = Clock.UtcNow
        };
        Repository.WireRequest(request);
        Repository.Requests.Add(request);
        return request;
    }

    public ShiftAssignment AddReceiverAssignment(DateOnly workDate, ShiftType shiftType, int id = 2)
    {
        var assignment = CreateAssignment(id, Receiver, workDate, shiftType);
        Repository.Assignments.Add(assignment);
        return assignment;
    }

    public void AddApprovedLeaveForReceiver(int? shiftTypeId = 1)
    {
        Repository.LeaveRequests.Add(new LeaveRequest
        {
            LeaveRequestId = 1,
            EmployeeId = Receiver.EmployeeId,
            SchedulePeriodId = Period.SchedulePeriodId,
            LeaveDate = TargetAssignment.WorkDate,
            ShiftTypeId = shiftTypeId,
            Status = LeaveRequestStatus.Approved
        });
    }

    public static Employee CreateEmployee(
        int id,
        int accountId,
        string code,
        string name,
        EmployeeLevel level,
        EmployeePosition position = EmployeePosition.Sales,
        EmploymentType employmentType = EmploymentType.PartTime)
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
            Position = position,
            EmploymentType = employmentType,
            Level = level,
            IsActive = true
        };
    }

    private ShiftAssignment CreateAssignment(
        int id,
        Employee employee,
        DateOnly workDate,
        ShiftType shiftType)
    {
        return new ShiftAssignment
        {
            ShiftAssignmentId = id,
            WorkScheduleId = Schedule.WorkScheduleId,
            WorkSchedule = Schedule,
            EmployeeId = employee.EmployeeId,
            Employee = employee,
            WorkDate = workDate,
            ShiftTypeId = shiftType.ShiftTypeId,
            ShiftType = shiftType,
            Source = AssignmentSource.Manual
        };
    }

    private Availability CreateAvailability(Employee employee, ShiftAssignment assignment)
    {
        return new Availability
        {
            AvailabilityId = 1,
            EmployeeId = employee.EmployeeId,
            SchedulePeriodId = Period.SchedulePeriodId,
            WorkDate = assignment.WorkDate,
            ShiftTypeId = assignment.ShiftTypeId
        };
    }

    private StaffingRequirement CreateRequirement(ShiftAssignment assignment)
    {
        return new StaffingRequirement
        {
            StaffingRequirementId = 1,
            SchedulePeriodId = Period.SchedulePeriodId,
            WorkDate = assignment.WorkDate,
            ShiftTypeId = assignment.ShiftTypeId,
            RequiredSalesStaff = 1,
            RequiredCashiers = 0,
            RequiredExperiencedStaff = 1,
            MinimumLevel = EmployeeLevel.Level2
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

    private static ShiftType CreateShift(
        int id,
        string code,
        string name,
        int startHour,
        int endHour)
    {
        return new ShiftType
        {
            ShiftTypeId = id,
            ShiftCode = code,
            ShiftName = name,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(endHour, 0),
            DurationHours = endHour >= startHour ? (endHour - startHour) : (endHour + 24 - startHour),
            DisplayOrder = id,
            IsActive = true
        };
    }
}
