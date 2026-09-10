using ShiftScheduling.Features.Reports.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.Reports;

public sealed class WorkReportCoverageTests
{
    private readonly FakeWorkScheduleRepository _workScheduleRepo = new();
    private readonly FakeSchedulePeriodRepository _periodRepo = new();
    private readonly FakeEmployeeRepository _employeeRepo = new();
    private readonly FakeLeaveRequestRepository _leaveRequestRepo = new();
    private readonly FakeAuditLogRepository _auditLogRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new();
    private readonly WorkReportService _service;

    public WorkReportCoverageTests()
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

    [Fact(DisplayName = "TC-NC08-001 - CoverageRate reaches 100% when all 42 required slots are fulfilled")]
    public async Task GetManagerReportOverviewAsync_WhenAllSlotsFulfilled_Returns100Percent()
    {
        var period = CreatePeriodWithStandard42Slots(1);
        _periodRepo.Add(period);
        var (salesEmp, cashierEmp) = SetupStandardEmployees();
        var schedule = CreatePublishedSchedule(1);
        AddAssignmentsForFullWeek(schedule, period.WeekStartDate, salesEmp, cashierEmp);
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(42, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(42, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(21, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(21, result.CoverageSummary.CoveredCashierSlots);
        Assert.Equal(100m, result.CoverageSummary.CoverageRate);
        Assert.True(result.CoverageSummary.IsFullyCovered);
        Assert.Equal("100%", result.CoverageSummary.FormattedCoverageRate);
    }

    [Fact(DisplayName = "TC-NC08-002 - CoverageRate reaches 50% when 21 of 42 required slots are fulfilled")]
    public async Task GetManagerReportOverviewAsync_WhenHalfSlotsFulfilled_Returns50Percent()
    {
        var period = CreatePeriodWithStandard42Slots(1);
        _periodRepo.Add(period);
        var (salesEmp, _) = SetupStandardEmployees();
        var schedule = CreatePublishedSchedule(1);
        AddSalesOnlyAssignments(schedule, period.WeekStartDate, salesEmp);
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(42, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(21, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(21, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(0, result.CoverageSummary.CoveredCashierSlots);
        Assert.Equal(50m, result.CoverageSummary.CoverageRate);
        Assert.False(result.CoverageSummary.IsFullyCovered);
        Assert.Equal("50%", result.CoverageSummary.FormattedCoverageRate);
    }

    [Fact(DisplayName = "TC-NC08-003 - CoverageRate caps at 100% and does not exceed when shifts are overstaffed")]
    public async Task GetManagerReportOverviewAsync_WhenOverstaffed_CapsAtRequiredCount()
    {
        var period = CreateSingleShiftPeriod(1, 1, 0);
        _periodRepo.Add(period);
        var s1 = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        var s2 = CreateEmployee(2, "S2", "Sales 2", EmployeePosition.Sales);
        var s3 = CreateEmployee(3, "S3", "Sales 3", EmployeePosition.Sales);
        _employeeRepo.Add(s1);
        _employeeRepo.Add(s2);
        _employeeRepo.Add(s3);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = 1, EmployeeId = 1, Employee = s1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 });
        schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = 2, EmployeeId = 2, Employee = s2, WorkDate = period.WeekStartDate, ShiftTypeId = 1 });
        schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = 3, EmployeeId = 3, Employee = s3, WorkDate = period.WeekStartDate, ShiftTypeId = 1 });
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(1, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(1, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(1, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(0, result.CoverageSummary.CoveredCashierSlots);
        Assert.Equal(100m, result.CoverageSummary.CoverageRate);
        Assert.True(result.CoverageSummary.IsFullyCovered);
        Assert.Equal("100%", result.CoverageSummary.FormattedCoverageRate);
    }

    [Fact(DisplayName = "TC-NC08-004 - Extra Sales assignments cannot compensate for missing Cashier")]
    public async Task GetManagerReportOverviewAsync_ExtraSalesDoesNotCoverMissingCashier()
    {
        var period = CreateSingleShiftPeriod(1, 1, 1);
        _periodRepo.Add(period);
        var s1 = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        var s2 = CreateEmployee(2, "S2", "Sales 2", EmployeePosition.Sales);
        _employeeRepo.Add(s1);
        _employeeRepo.Add(s2);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = 1, EmployeeId = 1, Employee = s1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 });
        schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = 2, EmployeeId = 2, Employee = s2, WorkDate = period.WeekStartDate, ShiftTypeId = 1 });
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(1, result.CoverageSummary.CoveredSalesSlots);
        Assert.Equal(0, result.CoverageSummary.CoveredCashierSlots);
        Assert.Equal(1, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(50m, result.CoverageSummary.CoverageRate);
    }

    [Fact(DisplayName = "TC-NC08-005 - Extra staff in Shift A does not compensate for missing staff in Shift B")]
    public async Task GetManagerReportOverviewAsync_ExtraStaffInOneShiftDoesNotCoverAnotherShift()
    {
        var date = new DateOnly(2026, 8, 17);
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Week 34",
            WeekStartDate = date,
            WeekEndDate = date.AddDays(6),
            StaffingRequirements = new List<StaffingRequirement>
            {
                new() { SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1, RequiredSalesStaff = 1, RequiredCashiers = 0 },
                new() { SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 2, RequiredSalesStaff = 1, RequiredCashiers = 0 }
            }
        };
        _periodRepo.Add(period);
        var s1 = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        var s2 = CreateEmployee(2, "S2", "Sales 2", EmployeePosition.Sales);
        _employeeRepo.Add(s1);
        _employeeRepo.Add(s2);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = 1, EmployeeId = 1, Employee = s1, WorkDate = date, ShiftTypeId = 1 });
        schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = 2, EmployeeId = 2, Employee = s2, WorkDate = date, ShiftTypeId = 1 });
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(2, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(1, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(50m, result.CoverageSummary.CoverageRate);
    }

    [Fact(DisplayName = "TC-NC08-006 - Period with no staffing requirements returns null rate and Not Applicable message")]
    public async Task GetManagerReportOverviewAsync_WhenNoRequirements_ReturnsNullRateAndNotApplicable()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Week Without Requirements",
            WeekStartDate = new DateOnly(2026, 8, 17),
            WeekEndDate = new DateOnly(2026, 8, 23),
            StaffingRequirements = new List<StaffingRequirement>()
        };
        _periodRepo.Add(period);
        var schedule = CreatePublishedSchedule(1);
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Null(result.CoverageSummary.CoverageRate);
        Assert.False(result.CoverageSummary.HasRequirements);
        Assert.False(result.CoverageSummary.IsFullyCovered);
        Assert.Equal("Không áp dụng", result.CoverageSummary.FormattedCoverageRate);
    }

    [Fact(DisplayName = "TC-NC08-007 - When no Published schedule exists, CoverageSummary is null")]
    public async Task GetManagerReportOverviewAsync_WhenNoPublishedSchedule_ReturnsNullCoverageSummary()
    {
        var period = CreatePeriodWithStandard42Slots(1);
        _periodRepo.Add(period);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result);
        Assert.Null(result.CoverageSummary);
    }

    [Fact(DisplayName = "TC-NC08-008 - Employee and position filters do not alter overall period coverage summary")]
    public async Task GetManagerReportOverviewAsync_WhenFilterApplied_CoverageSummaryRemainsUnchanged()
    {
        var period = CreatePeriodWithStandard42Slots(1);
        _periodRepo.Add(period);
        var (salesEmp, cashierEmp) = SetupStandardEmployees();
        var schedule = CreatePublishedSchedule(1);
        AddAssignmentsForFullWeek(schedule, period.WeekStartDate, salesEmp, cashierEmp);
        _workScheduleRepo.Schedules.Add(schedule);

        var unfiltered = await _service.GetManagerReportOverviewAsync(1);
        var filteredByEmp = await _service.GetManagerReportOverviewAsync(1, employeeIdFilter: salesEmp.EmployeeId);
        var filteredByPos = await _service.GetManagerReportOverviewAsync(1, positionFilter: EmployeePosition.Cashier);

        Assert.NotNull(unfiltered?.CoverageSummary);
        Assert.NotNull(filteredByEmp?.CoverageSummary);
        Assert.NotNull(filteredByPos?.CoverageSummary);
        Assert.Equal(unfiltered.CoverageSummary.CoverageRate, filteredByEmp.CoverageSummary.CoverageRate);
        Assert.Equal(unfiltered.CoverageSummary.TotalCoveredSlots, filteredByPos.CoverageSummary.TotalCoveredSlots);
    }

    [Fact(DisplayName = "TC-NC08-009 - Reassigned shift assignment still counts as exactly one covered slot")]
    public async Task GetManagerReportOverviewAsync_WhenShiftTransferred_SlotIsCountedOnce()
    {
        var period = CreateSingleShiftPeriod(1, 1, 0);
        _periodRepo.Add(period);
        var sales2 = CreateEmployee(2, "S2", "New Assignee", EmployeePosition.Sales);
        _employeeRepo.Add(sales2);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 1,
            EmployeeId = sales2.EmployeeId,
            Employee = sales2,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = 1,
            Source = AssignmentSource.ShiftTransfer
        });
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(1, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(100m, result.CoverageSummary.CoverageRate);
    }

    [Fact(DisplayName = "TC-NC08-010 - Only assignments matching date and shift within period are counted towards coverage")]
    public async Task GetManagerReportOverviewAsync_OnlyMatchingPeriodAssignmentsCounted()
    {
        var date = new DateOnly(2026, 8, 17);
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Period 1",
            WeekStartDate = date,
            WeekEndDate = date.AddDays(6),
            StaffingRequirements = new List<StaffingRequirement>
            {
                new() { SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = 1, RequiredSalesStaff = 1, RequiredCashiers = 0 }
            }
        };
        _periodRepo.Add(period);
        var sales1 = CreateEmployee(1, "S1", "Sales 1", EmployeePosition.Sales);
        _employeeRepo.Add(sales1);

        var schedule = CreatePublishedSchedule(1);
        schedule.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 1,
            EmployeeId = sales1.EmployeeId,
            Employee = sales1,
            WorkDate = date.AddDays(10),
            ShiftTypeId = 1
        });
        _workScheduleRepo.Schedules.Add(schedule);

        var result = await _service.GetManagerReportOverviewAsync(1);

        Assert.NotNull(result?.CoverageSummary);
        Assert.Equal(1, result.CoverageSummary.TotalRequiredSlots);
        Assert.Equal(0, result.CoverageSummary.TotalCoveredSlots);
        Assert.Equal(0m, result.CoverageSummary.CoverageRate);
    }

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

    private static SchedulePeriod CreatePeriodWithStandard42Slots(int periodId)
    {
        var startDate = new DateOnly(2026, 8, 17);
        var requirements = new List<StaffingRequirement>();
        for (var day = 0; day < 7; day++)
        {
            var workDate = startDate.AddDays(day);
            for (var shiftId = 1; shiftId <= 3; shiftId++)
            {
                requirements.Add(new StaffingRequirement
                {
                    SchedulePeriodId = periodId,
                    WorkDate = workDate,
                    ShiftTypeId = shiftId,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }
        return new SchedulePeriod
        {
            SchedulePeriodId = periodId,
            PeriodName = $"Kỳ lịch {periodId}",
            WeekStartDate = startDate,
            WeekEndDate = startDate.AddDays(6),
            StaffingRequirements = requirements
        };
    }

    private static SchedulePeriod CreateSingleShiftPeriod(int periodId, int salesReq, int cashierReq)
    {
        var startDate = new DateOnly(2026, 8, 17);
        return new SchedulePeriod
        {
            SchedulePeriodId = periodId,
            PeriodName = $"Single Shift Period {periodId}",
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
    }

    private (Employee Sales, Employee Cashier) SetupStandardEmployees()
    {
        var sales = CreateEmployee(1, "EMP001", "Nguyễn Văn Sales", EmployeePosition.Sales);
        var cashier = CreateEmployee(2, "EMP002", "Trần Thị Cashier", EmployeePosition.Cashier);
        _employeeRepo.Add(sales);
        _employeeRepo.Add(cashier);
        return (sales, cashier);
    }

    private static WorkSchedule CreatePublishedSchedule(int periodId) => new()
    {
        WorkScheduleId = periodId,
        SchedulePeriodId = periodId,
        ScheduleName = $"Lịch kỳ {periodId}",
        Status = ScheduleStatus.Published,
        ShiftAssignments = new List<ShiftAssignment>()
    };

    private static void AddAssignmentsForFullWeek(WorkSchedule schedule, DateOnly startDate, Employee sales, Employee cashier)
    {
        var id = 1;
        for (var day = 0; day < 7; day++)
        {
            var workDate = startDate.AddDays(day);
            for (var shiftId = 1; shiftId <= 3; shiftId++)
            {
                schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = id++, WorkScheduleId = schedule.WorkScheduleId, EmployeeId = sales.EmployeeId, Employee = sales, WorkDate = workDate, ShiftTypeId = shiftId });
                schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = id++, WorkScheduleId = schedule.WorkScheduleId, EmployeeId = cashier.EmployeeId, Employee = cashier, WorkDate = workDate, ShiftTypeId = shiftId });
            }
        }
    }

    private static void AddSalesOnlyAssignments(WorkSchedule schedule, DateOnly startDate, Employee sales)
    {
        var id = 1;
        for (var day = 0; day < 7; day++)
        {
            var workDate = startDate.AddDays(day);
            for (var shiftId = 1; shiftId <= 3; shiftId++)
            {
                schedule.ShiftAssignments.Add(new ShiftAssignment { ShiftAssignmentId = id++, WorkScheduleId = schedule.WorkScheduleId, EmployeeId = sales.EmployeeId, Employee = sales, WorkDate = workDate, ShiftTypeId = shiftId });
            }
        }
    }
}
