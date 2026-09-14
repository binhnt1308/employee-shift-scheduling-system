using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Reports;

public sealed class WorkReportCoverageLeaveTests
{
    private readonly FakeWorkScheduleRepository _workScheduleRepo = new();
    private readonly FakeSchedulePeriodRepository _periodRepo = new();
    private readonly FakeEmployeeRepository _employeeRepo = new();
    private readonly FakeLeaveRequestRepository _leaveRequestRepo = new();
    private readonly FakeAuditLogRepository _auditLogRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new();
    private readonly WorkReportService _service;

    public WorkReportCoverageLeaveTests()
    {
        _service = new WorkReportService(
            _workScheduleRepo,
            _periodRepo,
            _employeeRepo,
            _leaveRequestRepo,
            _auditLogRepo,
            _unitOfWork,
            _clock);
    }

    [Fact(DisplayName = "TC-REPORT-012 - Đơn nghỉ đúng ca đã duyệt không được tính vào độ phủ nhân sự của ca đó")]
    public async Task GetManagerReportOverviewAsync_WhenApprovedExactShiftLeave_ExcludesFromCoverage()
    {
        // Arrange
        var date = new DateOnly(2026, 8, 17);
        _periodRepo.Add(CreateSingleShiftPeriod(1, date, salesReq: 1, cashierReq: 0));
        var salesEmp = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        _employeeRepo.Add(salesEmp);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(CreateAssignment(1, salesEmp, date, 1));
        _workScheduleRepo.Schedules.Add(schedule);

        AddLeave(101, salesEmp.EmployeeId, date, 1, LeaveRequestStatus.Approved);

        // Act
        var result = await _service.GetManagerReportOverviewAsync(1);

        // Assert
        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(1, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(1, result.CoverageSummary.RequiredSalesSlots);
        Assert.Equal(0, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(0, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(0m, result.CoverageSummary.CoverageRate);
        Assert.False(result.CoverageSummary.IsFullyCovered);
    }

    [Fact(DisplayName = "TC-REPORT-013 - Đơn nghỉ cả ngày đã duyệt không được tính vào độ phủ của các ca trong ngày")]
    public async Task GetManagerReportOverviewAsync_WhenApprovedFullDayLeave_ExcludesFromAllShiftsOnThatDate()
    {
        // Arrange
        var date = new DateOnly(2026, 8, 17);
        _periodRepo.Add(CreateTwoShiftPeriod(1, date, salesReq: 1));
        var salesEmp = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        _employeeRepo.Add(salesEmp);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(CreateAssignment(1, salesEmp, date, 1));
        schedule.ShiftAssignments.Add(CreateAssignment(2, salesEmp, date, 2));
        _workScheduleRepo.Schedules.Add(schedule);

        AddLeave(101, salesEmp.EmployeeId, date, null, LeaveRequestStatus.Approved);

        // Act
        var result = await _service.GetManagerReportOverviewAsync(1);

        // Assert
        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(2, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(2, result.CoverageSummary.RequiredSalesSlots);
        Assert.Equal(0, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(0, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(0m, result.CoverageSummary.CoverageRate);
        Assert.False(result.CoverageSummary.IsFullyCovered);
    }

    [Theory(DisplayName = "TC-REPORT-014 - Đơn nghỉ chờ duyệt hoặc bị từ chối không làm giảm độ phủ nhân sự")]
    [InlineData(LeaveRequestStatus.Pending)]
    [InlineData(LeaveRequestStatus.Rejected)]
    public async Task GetManagerReportOverviewAsync_WhenLeavePendingOrRejected_DoesNotReduceCoverage(LeaveRequestStatus status)
    {
        // Arrange
        var date = new DateOnly(2026, 8, 17);
        _periodRepo.Add(CreateSingleShiftPeriod(1, date, salesReq: 1, cashierReq: 0));
        var salesEmp = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        _employeeRepo.Add(salesEmp);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(CreateAssignment(1, salesEmp, date, 1));
        _workScheduleRepo.Schedules.Add(schedule);

        AddLeave(101, salesEmp.EmployeeId, date, 1, status);

        // Act
        var result = await _service.GetManagerReportOverviewAsync(1);

        // Assert
        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(1, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(1, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(1, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(100m, result.CoverageSummary.CoverageRate);
        Assert.True(result.CoverageSummary.IsFullyCovered);
    }

    [Theory(DisplayName = "TC-REPORT-015 - Đơn nghỉ của nhân viên khác hoặc ca khác không làm giảm độ phủ của ca hiện tại")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetManagerReportOverviewAsync_WhenApprovedLeaveForDifferentEmployeeOrShift_DoesNotReduceCoverage(
        bool differentEmployee, bool differentShift)
    {
        // Arrange
        var date = new DateOnly(2026, 8, 17);
        _periodRepo.Add(CreateSingleShiftPeriod(1, date, salesReq: 1, cashierReq: 0));
        var sales1 = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        var sales2 = CreateEmployee(2, "S2", "Sales 2", EmployeePosition.Sales);
        _employeeRepo.Add(sales1);
        _employeeRepo.Add(sales2);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(CreateAssignment(1, sales1, date, 1));
        _workScheduleRepo.Schedules.Add(schedule);

        int leaveEmployeeId = differentEmployee ? sales2.EmployeeId : sales1.EmployeeId;
        int? leaveShiftTypeId = differentShift ? 2 : 1;
        AddLeave(101, leaveEmployeeId, date, leaveShiftTypeId, LeaveRequestStatus.Approved);

        // Act
        var result = await _service.GetManagerReportOverviewAsync(1);

        // Assert
        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(1, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(1, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(1, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(100m, result.CoverageSummary.CoverageRate);
        Assert.True(result.CoverageSummary.IsFullyCovered);
    }

    [Theory(DisplayName = "TC-REPORT-016 - Độ phủ sau chuyển ca được tính theo nhân viên hiện đang nhận ca")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetManagerReportOverviewAsync_WhenShiftTransferred_RecipientLeaveAffectsCoverageWhileSenderLeaveDoesNot(
        bool recipientHasApprovedLeave)
    {
        // Arrange
        var date = new DateOnly(2026, 8, 17);
        _periodRepo.Add(CreateSingleShiftPeriod(1, date, salesReq: 1, cashierReq: 0));
        var sender = CreateEmployee(1, "S1", "Sender Sales", EmployeePosition.Sales);
        var recipient = CreateEmployee(2, "R2", "Recipient Sales", EmployeePosition.Sales);
        _employeeRepo.Add(sender);
        _employeeRepo.Add(recipient);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(CreateAssignment(1, recipient, date, 1, AssignmentSource.ShiftTransfer));
        _workScheduleRepo.Schedules.Add(schedule);

        int leaveEmployeeId = recipientHasApprovedLeave ? recipient.EmployeeId : sender.EmployeeId;
        AddLeave(101, leaveEmployeeId, date, 1, LeaveRequestStatus.Approved);

        // Act
        var result = await _service.GetManagerReportOverviewAsync(1);

        // Assert
        Assert.NotNull(result?.CoverageSummary);
        int expectedCovered = recipientHasApprovedLeave ? 0 : 1;
        decimal expectedRate = recipientHasApprovedLeave ? 0m : 100m;
        bool expectedIsFullyCovered = !recipientHasApprovedLeave;

        Assert.Equal(expectedCovered, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(expectedRate, result.CoverageSummary.CoverageRate);
        Assert.Equal(expectedIsFullyCovered, result.CoverageSummary.IsFullyCovered);
    }

    private void AddLeave(int id, int employeeId, DateOnly date, int? shiftTypeId, LeaveRequestStatus status)
    {
        _leaveRequestRepo.Requests.Add(new LeaveRequest
        {
            LeaveRequestId = id,
            EmployeeId = employeeId,
            SchedulePeriodId = 1,
            LeaveDate = date,
            ShiftTypeId = shiftTypeId,
            Status = status
        });
    }

    private static ShiftAssignment CreateAssignment(
        int id, Employee emp, DateOnly date, int shiftTypeId, AssignmentSource source = AssignmentSource.Manual) =>
        new()
        {
            ShiftAssignmentId = id,
            EmployeeId = emp.EmployeeId,
            Employee = emp,
            WorkDate = date,
            ShiftTypeId = shiftTypeId,
            Source = source
        };

    private static Employee CreateEmployee(int id, string code, string name, EmployeePosition position) => new()
    {
        EmployeeId = id,
        EmployeeCode = code,
        FullName = name,
        Position = position,
        EmploymentType = EmploymentType.FullTime,
        Level = EmployeeLevel.Level2,
        IsActive = true
    };

    private static SchedulePeriod CreateSingleShiftPeriod(int periodId, DateOnly startDate, int salesReq, int cashierReq) => new()
    {
        SchedulePeriodId = periodId,
        PeriodName = $"Kỳ {periodId}",
        WeekStartDate = startDate,
        WeekEndDate = startDate.AddDays(6),
        StaffingRequirements = new List<StaffingRequirement>
        {
            new()
            {
                SchedulePeriodId = periodId,
                WorkDate = startDate,
                ShiftTypeId = 1,
                RequiredSalesStaff = salesReq,
                RequiredCashiers = cashierReq
            }
        }
    };

    private static SchedulePeriod CreateTwoShiftPeriod(int periodId, DateOnly startDate, int salesReq) => new()
    {
        SchedulePeriodId = periodId,
        PeriodName = $"Kỳ {periodId}",
        WeekStartDate = startDate,
        WeekEndDate = startDate.AddDays(6),
        StaffingRequirements = new List<StaffingRequirement>
        {
            new() { SchedulePeriodId = periodId, WorkDate = startDate, ShiftTypeId = 1, RequiredSalesStaff = salesReq, RequiredCashiers = 0 },
            new() { SchedulePeriodId = periodId, WorkDate = startDate, ShiftTypeId = 2, RequiredSalesStaff = salesReq, RequiredCashiers = 0 }
        }
    };

    private static WorkSchedule CreatePublishedSchedule(int periodId) => new()
    {
        WorkScheduleId = periodId,
        SchedulePeriodId = periodId,
        ScheduleName = $"Lịch {periodId}",
        Status = ScheduleStatus.Published,
        ShiftAssignments = new List<ShiftAssignment>()
    };
}
