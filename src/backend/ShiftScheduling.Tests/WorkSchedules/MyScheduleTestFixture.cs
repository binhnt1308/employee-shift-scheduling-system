using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.WorkSchedules;

// Fixture dựng dữ liệu lịch cá nhân để kiểm thử UC08 nhất quán giữa nhiều kịch bản.
internal static class MyScheduleTestFixtureFactory
{
    public static MyScheduleFixture CreateFixture(DateOnly today)
    {
        var workScheduleRepo = new FakeWorkScheduleRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var employeeRepo = new FakeEmployeeRepository();
        var availRepo = new FakeAvailabilityRepository();
        var leaveRepo = new FakeLeaveRequestRepository();
        var clock = new FakeClock(today.ToDateTime(new TimeOnly(2, 0, 0), DateTimeKind.Utc));

        var (currentAccount, currentEmployee) = CreateCurrentAccountAndEmployee(employeeRepo);
        var (morning, afternoon, evening) = CreateDefaultShiftTypes(shiftTypeRepo);

        var service = CreateWorkScheduleService(
            workScheduleRepo, periodRepo, shiftTypeRepo, employeeRepo, availRepo, leaveRepo, clock);

        return new MyScheduleFixture(
            service, workScheduleRepo, periodRepo, shiftTypeRepo, employeeRepo, leaveRepo,
            currentAccount, currentEmployee, morning, afternoon, evening);
    }

    private static (UserAccount Account, Employee Employee) CreateCurrentAccountAndEmployee(
        FakeEmployeeRepository employeeRepo)
    {
        var currentAccount = new UserAccount
        {
            UserAccountId = 10,
            Username = "nhanvien1",
            Role = UserRole.Employee,
            IsLocked = false
        };
        var currentEmployee = new Employee
        {
            EmployeeId = 1,
            UserAccountId = 10,
            EmployeeCode = "NV01",
            FullName = "Nguyễn Văn Một",
            Email = "nv1@example.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };
        employeeRepo.Employees.Add(currentEmployee);
        return (currentAccount, currentEmployee);
    }

    private static WorkScheduleService CreateWorkScheduleService(
        FakeWorkScheduleRepository workScheduleRepo,
        FakeSchedulePeriodRepository periodRepo,
        FakeShiftTypeRepository shiftTypeRepo,
        FakeEmployeeRepository employeeRepo,
        FakeAvailabilityRepository availRepo,
        FakeLeaveRequestRepository leaveRepo,
        FakeClock clock)
    {
        var validationService = new SchedulingValidationService();
        var orToolsService = new OrToolsSchedulingService(validationService, NullLogger<OrToolsSchedulingService>.Instance);

        return new WorkScheduleService(
            workScheduleRepo, periodRepo, shiftTypeRepo, employeeRepo, availRepo, leaveRepo,
            validationService, orToolsService, new FakeNotificationRepository(), new FakeAuditLogRepository(),
            new FakeUnitOfWork(), clock, NullLogger<WorkScheduleService>.Instance, new FakeEmailSender());
    }

    private static (ShiftType Morning, ShiftType Afternoon, ShiftType Evening) CreateDefaultShiftTypes(
        FakeShiftTypeRepository shiftTypeRepo)
    {
        var morning = new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca Sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DisplayOrder = 1,
            IsActive = true
        };
        var afternoon = new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca Chiều",
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(16, 0),
            DisplayOrder = 2,
            IsActive = true
        };
        var evening = new ShiftType
        {
            ShiftTypeId = 3,
            ShiftCode = "EVENING",
            ShiftName = "Ca Tối",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(20, 0),
            DisplayOrder = 3,
            IsActive = true
        };
        shiftTypeRepo.ShiftTypes.AddRange([morning, afternoon, evening]);
        return (morning, afternoon, evening);
    }
}

internal sealed class MyScheduleFixture
{
    public WorkScheduleService Service { get; }
    public FakeWorkScheduleRepository WorkScheduleRepo { get; }
    public FakeSchedulePeriodRepository PeriodRepo { get; }
    public FakeShiftTypeRepository ShiftTypeRepo { get; }
    public FakeEmployeeRepository EmployeeRepo { get; }
    public FakeLeaveRequestRepository LeaveRepo { get; }
    public UserAccount CurrentAccount { get; }
    public Employee CurrentEmployee { get; }
    public ShiftType Morning { get; }
    public ShiftType Afternoon { get; }
    public ShiftType Evening { get; }

    public MyScheduleFixture(
        WorkScheduleService service,
        FakeWorkScheduleRepository workScheduleRepo,
        FakeSchedulePeriodRepository periodRepo,
        FakeShiftTypeRepository shiftTypeRepo,
        FakeEmployeeRepository employeeRepo,
        FakeLeaveRequestRepository leaveRepo,
        UserAccount currentAccount,
        Employee currentEmployee,
        ShiftType morning,
        ShiftType afternoon,
        ShiftType evening)
    {
        Service = service;
        WorkScheduleRepo = workScheduleRepo;
        PeriodRepo = periodRepo;
        ShiftTypeRepo = shiftTypeRepo;
        EmployeeRepo = employeeRepo;
        LeaveRepo = leaveRepo;
        CurrentAccount = currentAccount;
        CurrentEmployee = currentEmployee;
        Morning = morning;
        Afternoon = afternoon;
        Evening = evening;
    }

    public SchedulePeriod CreatePeriod(int id, string name, DateOnly startDate, DateOnly endDate)
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = id,
            PeriodName = name,
            WeekStartDate = startDate,
            WeekEndDate = endDate,
            AvailabilityDeadline = startDate.ToDateTime(new TimeOnly(0, 0)).AddDays(-1),
            RegistrationStatus = RegistrationStatus.Closed,
            StaffingRequirements = new List<StaffingRequirement>()
        };
        PeriodRepo.SchedulePeriods.Add(period);
        return period;
    }

    public WorkSchedule CreateSchedule(int id, SchedulePeriod period, ScheduleStatus status, DateTime? publishedAt)
    {
        var schedule = new WorkSchedule
        {
            WorkScheduleId = id,
            SchedulePeriodId = period.SchedulePeriodId,
            SchedulePeriod = period,
            ScheduleName = $"Lịch {period.PeriodName}",
            Status = status,
            PublishedAt = publishedAt,
            PublishedByAccountId = status == ScheduleStatus.Published ? 2 : null,
            ShiftAssignments = new List<ShiftAssignment>()
        };
        WorkScheduleRepo.Schedules.Add(schedule);
        if (!period.WorkSchedules.Contains(schedule))
        {
            period.WorkSchedules.Add(schedule);
        }
        return schedule;
    }

    public void AddAssignment(WorkSchedule schedule, Employee emp, DateOnly date, int shiftTypeId)
    {
        var assignment = new ShiftAssignment
        {
            ShiftAssignmentId = schedule.ShiftAssignments.Count + 1,
            WorkScheduleId = schedule.WorkScheduleId,
            WorkSchedule = schedule,
            EmployeeId = emp.EmployeeId,
            Employee = emp,
            WorkDate = date,
            ShiftTypeId = shiftTypeId,
            ShiftType = shiftTypeId == 1 ? Morning : (shiftTypeId == 2 ? Afternoon : Evening),
            Source = AssignmentSource.Manual
        };
        schedule.ShiftAssignments.Add(assignment);
    }

    public LeaveRequest AddLeaveRequest(int periodId, Employee emp, DateOnly date, int? shiftTypeId, LeaveRequestStatus status)
    {
        var req = new LeaveRequest
        {
            LeaveRequestId = LeaveRepo.Requests.Count + 1,
            SchedulePeriodId = periodId,
            EmployeeId = emp.EmployeeId,
            Employee = emp,
            LeaveDate = date,
            ShiftTypeId = shiftTypeId,
            Status = status,
            Reason = "Lý do xin nghỉ",
            SubmittedAt = DateTime.UtcNow
        };
        LeaveRepo.Requests.Add(req);
        return req;
    }
}
