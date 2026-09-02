using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class WorkScheduleIntegrationTests
{
    private readonly FakeWorkScheduleRepository _workScheduleRepo = new();
    private readonly FakeSchedulePeriodRepository _periodRepo = new();
    private readonly FakeShiftTypeRepository _shiftTypeRepo = new();
    private readonly FakeEmployeeRepository _employeeRepo = new();
    private readonly FakeAvailabilityRepository _availRepo = new();
    private readonly FakeLeaveRequestRepository _leaveRepo = new();
    private readonly FakeNotificationRepository _notificationRepo = new();
    private readonly FakeAuditLogRepository _auditLogRepo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new();
    private readonly SchedulingValidationService _validationService = new();
    private readonly OrToolsSchedulingService _orToolsService;
    private readonly WorkScheduleService _scheduleService;

    public WorkScheduleIntegrationTests()
    {
        _orToolsService = new OrToolsSchedulingService(
            _validationService,
            NullLogger<OrToolsSchedulingService>.Instance);

        _scheduleService = new WorkScheduleService(
            _workScheduleRepo,
            _periodRepo,
            _shiftTypeRepo,
            _employeeRepo,
            _availRepo,
            _leaveRepo,
            _validationService,
            _orToolsService,
            _notificationRepo,
            _auditLogRepo,
            _unitOfWork,
            _clock,
            NullLogger<WorkScheduleService>.Instance);
    }

    [Fact(DisplayName = "TC05-INT-01 - UC05 BR21 - AssignEmployeeManualAsync vi phạm 24h nghỉ -> Trả Failure")]
    public async Task AssignEmployeeManualAsync_WhenViolatesBR21_ReturnsFailureAndDoesNotSave()
    {
        var (period, emp, morning) = await SetupScheduleWithSixDaysShiftAsync();

        var model = new ManualAssignRequestViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        };

        var result = await _scheduleService.AssignEmployeeManualAsync(model, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftAssignment, result.ErrorCode);
        Assert.Contains("BR21", result.ErrorMessage);
        Assert.Equal(0, _workScheduleRepo.SaveAssignmentsCount);
        Assert.DoesNotContain(_workScheduleRepo.SavedAssignments, a => a.WorkDate == period.WeekStartDate);
    }

    private async Task<(SchedulePeriod Period, Employee Emp, ShiftType Morning)> SetupScheduleWithSixDaysShiftAsync()
    {
        var period = CreateStandardPeriod();
        _periodRepo.SchedulePeriods.Add(period);

        var emp = CreateEmployee(1, EmploymentType.PartTime);
        _employeeRepo.Employees.Add(emp);

        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12);
        _shiftTypeRepo.ShiftTypes.Add(morning);

        var draft = await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);

        for (int d = 1; d <= 6; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            draft.ShiftAssignments.Add(new ShiftAssignment
            {
                ShiftAssignmentId = d,
                WorkScheduleId = draft.WorkScheduleId,
                EmployeeId = emp.EmployeeId,
                WorkDate = date,
                ShiftTypeId = morning.ShiftTypeId,
                Source = AssignmentSource.Manual,
                WorkSchedule = draft
            });
        }

        _availRepo.Availabilities.Add(new Availability
        {
            EmployeeId = emp.EmployeeId,
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId
        });

        return (period, emp, morning);
    }

    [Fact(DisplayName = "TC07-INT-01 - UC07 BR21 - PublishScheduleAsync có vi phạm BR21" +
        " -> Chặn công bố & Giữ Draft")]
    public async Task PublishScheduleAsync_WhenScheduleViolatesBR21_BlocksPublishingAndRemainsDraft()
    {
        var period = CreateStandardPeriod();
        _periodRepo.SchedulePeriods.Add(period);

        var emp = CreateEmployee(1, EmploymentType.PartTime);
        _employeeRepo.Employees.Add(emp);

        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12);
        _shiftTypeRepo.ShiftTypes.Add(morning);

        var draft = await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);

        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            draft.ShiftAssignments.Add(new ShiftAssignment
            {
                ShiftAssignmentId = d + 1,
                WorkScheduleId = draft.WorkScheduleId,
                EmployeeId = emp.EmployeeId,
                WorkDate = date,
                ShiftTypeId = morning.ShiftTypeId,
                Source = AssignmentSource.Manual,
                WorkSchedule = draft
            });

            _availRepo.Availabilities.Add(new Availability
            {
                EmployeeId = emp.EmployeeId,
                SchedulePeriodId = period.SchedulePeriodId,
                WorkDate = date,
                ShiftTypeId = morning.ShiftTypeId
            });
        }

        var result = await _scheduleService.PublishScheduleAsync(period.SchedulePeriodId, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePublish, result.ErrorCode);
        Assert.Contains("BR21", result.ErrorMessage);
        Assert.Equal(0, _workScheduleRepo.PublishScheduleCount);
        Assert.Equal(ScheduleStatus.Draft, draft.Status);
    }

    [Fact(DisplayName = "TC07-INT-02 - UC07 BR02 - Employee inactive sau khi được xếp Draft -> chặn Publish")]
    public async Task PublishScheduleAsync_WhenDraftContainsInactiveEmployee_BlocksPublishingAndRemainsDraft()
    {
        var period = CreateStandardPeriod();
        _periodRepo.SchedulePeriods.Add(period);

        var emp = CreateEmployee(1, EmploymentType.PartTime);
        _employeeRepo.Employees.Add(emp);

        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12);
        _shiftTypeRepo.ShiftTypes.Add(morning);

        var draft = await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);
        draft.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftAssignmentId = 1,
            WorkScheduleId = draft.WorkScheduleId,
            EmployeeId = emp.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId,
            Source = AssignmentSource.Manual,
            WorkSchedule = draft
        });

        emp.IsActive = false;

        var result = await _scheduleService.PublishScheduleAsync(period.SchedulePeriodId, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePublish, result.ErrorCode);
        Assert.Contains("BR02", result.ErrorMessage);
        Assert.Equal(0, _workScheduleRepo.PublishScheduleCount);
        Assert.Equal(ScheduleStatus.Draft, draft.Status);
    }

    [Fact(DisplayName = "TC06-INT-01 - UC06 BR15 - GenerateOrToolsProposalAsync" +
        " chỉ sinh proposal, không auto-publish")]
    public async Task GenerateOrToolsProposalAsync_BR15_ReturnsProposalWithoutAutoPublishingOrSaving()
    {
        var period = CreateStandardPeriod();
        _periodRepo.SchedulePeriods.Add(period);

        var shiftTypes = new List<ShiftType>
        {
            CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12),
            CreateShiftType(2, "AFTERNOON", "Ca Chiều", 12, 16),
            CreateShiftType(3, "EVENING", "Ca Tối", 16, 20)
        };
        _shiftTypeRepo.ShiftTypes.AddRange(shiftTypes);

        var employees = new List<Employee>
        {
            CreateEmployee(1, EmploymentType.PartTime, EmployeePosition.Sales),
            CreateEmployee(2, EmploymentType.PartTime, EmployeePosition.Cashier)
        };
        _employeeRepo.Employees.AddRange(employees);

        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            foreach (var st in shiftTypes)
            {
                foreach (var emp in employees)
                {
                    _availRepo.Availabilities.Add(new Availability
                    {
                        EmployeeId = emp.EmployeeId,
                        SchedulePeriodId = period.SchedulePeriodId,
                        WorkDate = date,
                        ShiftTypeId = st.ShiftTypeId
                    });
                }
            }
        }

        var result = await _scheduleService.GenerateOrToolsProposalAsync(period.SchedulePeriodId, 2);

        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Equal(0, _workScheduleRepo.PublishScheduleCount);
        Assert.Equal(0, _workScheduleRepo.SaveAssignmentsCount);
        Assert.Contains(_auditLogRepo.Items, log => log.Action == "GENERATE_ORTOOLS_PROPOSAL");

        var scheduleInRepo = await _workScheduleRepo.GetByPeriodIdAsync(period.SchedulePeriodId, true, true);
        Assert.True(scheduleInRepo is null || scheduleInRepo.Status != ScheduleStatus.Published);
    }

    [Fact(DisplayName = "TC07-INT-03 - Gọi luồng công bố với lịch thiếu định mức -> Thất bại, trạng thái vẫn là nháp, không tạo thông báo")]
    public async Task PublishScheduleAsync_WhenUnderstaffed_FailsRemainsDraftAndNoNotificationsCreated()
    {
        var period = CreateStandardPeriod();
        _periodRepo.SchedulePeriods.Add(period);

        var morning = CreateShiftType(1, "MORNING", "Ca Sáng", 8, 12);
        _shiftTypeRepo.ShiftTypes.Add(morning);

        period.StaffingRequirements.Add(new StaffingRequirement
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = morning.ShiftTypeId,
            ShiftType = morning,
            RequiredSalesStaff = 1,
            RequiredCashiers = 0
        });

        var draft = await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);

        var result = await _scheduleService.PublishScheduleAsync(period.SchedulePeriodId, accountId: 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePublish, result.ErrorCode);
        Assert.Contains("vi phạm bắt buộc", result.ErrorMessage);
        Assert.Equal(0, _workScheduleRepo.PublishScheduleCount);
        Assert.Equal(ScheduleStatus.Draft, draft.Status);
        Assert.Empty(_notificationRepo.Items);
    }

    private static SchedulePeriod CreateStandardPeriod() => new()
    {
        SchedulePeriodId = 1,
        PeriodName = "Tuần 10/08 - 16/08",
        WeekStartDate = new DateOnly(2026, 8, 10),
        WeekEndDate = new DateOnly(2026, 8, 16),
        RegistrationStatus = RegistrationStatus.Closed,
        StaffingRequirements = new List<StaffingRequirement>()
    };

    private static Employee CreateEmployee(
        int id,
        EmploymentType type,
        EmployeePosition pos = EmployeePosition.Sales) => new()
        {
            EmployeeId = id,
            UserAccountId = id + 10,
            EmployeeCode = $"NV{id:D2}",
            FullName = $"Nhân viên {id}",
            EmploymentType = type,
            Position = pos,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };

    private static ShiftType CreateShiftType(
        int id, string code, string name, int start, int end) => new()
        {
            ShiftTypeId = id,
            ShiftCode = code,
            ShiftName = name,
            StartTime = new TimeOnly(start, 0),
            EndTime = new TimeOnly(end, 0),
            DurationHours = end - start,
            DisplayOrder = id,
            IsActive = true
        };

    [Fact(DisplayName = "TC05-INT-09 - GetEditGridAsync với kỳ dùng ShiftType cũ (IsActive = false) thiếu định mức -> IsValid = false, HardErrors không rỗng")]
    public async Task GetEditGridAsync_WhenPeriodUsesOldInactiveShiftsAndUnderstaffed_ReturnsInvalidValidationResult()
    {
        var period = CreateStandardPeriod();
        _periodRepo.Add(period);

        // Version 1 (cũ): IsActive = false
        var oldMorning = CreateShiftType(1, "MORNING", "Ca Sáng (cũ)", 8, 12);
        oldMorning.IsActive = false;
        var oldAfternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều (cũ)", 12, 16);
        oldAfternoon.IsActive = false;
        var oldEvening = CreateShiftType(3, "EVENING", "Ca Tối (cũ)", 16, 20);
        oldEvening.IsActive = false;

        // Version 2 (mới): IsActive = true
        var newMorning = CreateShiftType(4, "MORNING", "Ca Sáng (mới)", 7, 11);
        newMorning.IsActive = true;
        var newAfternoon = CreateShiftType(5, "AFTERNOON", "Ca Chiều (mới)", 11, 15);
        newAfternoon.IsActive = true;
        var newEvening = CreateShiftType(6, "EVENING", "Ca Tối (mới)", 15, 19);
        newEvening.IsActive = true;

        _shiftTypeRepo.ShiftTypes.AddRange(new[] { oldMorning, oldAfternoon, oldEvening, newMorning, newAfternoon, newEvening });

        // Kỳ chỉ có StaffingRequirements cho ca cũ (1, 2, 3)
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = oldMorning.ShiftTypeId,
            ShiftType = oldMorning,
            RequiredSalesStaff = 2,
            RequiredCashiers = 1
        });
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = oldAfternoon.ShiftTypeId,
            ShiftType = oldAfternoon,
            RequiredSalesStaff = 2,
            RequiredCashiers = 1
        });
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = oldEvening.ShiftTypeId,
            ShiftType = oldEvening,
            RequiredSalesStaff = 1,
            RequiredCashiers = 1
        });

        // Chỉ có 1 nhân viên Sales Level 1 và 1 phân công
        var emp1 = CreateEmployee(1, EmploymentType.FullTime, EmployeePosition.Sales);
        emp1.Level = EmployeeLevel.Level1;
        _employeeRepo.Add(emp1);

        var draft = await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);
        draft.ShiftAssignments.Add(new ShiftAssignment
        {
            WorkScheduleId = draft.WorkScheduleId,
            EmployeeId = emp1.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = oldMorning.ShiftTypeId
        });

        var model = await _scheduleService.GetEditGridAsync(period.SchedulePeriodId, 2);

        Assert.NotNull(model);
        Assert.False(model.ValidationResult.IsValid);
        Assert.NotEmpty(model.ValidationResult.HardErrors);
        Assert.Contains(model.ValidationResult.HardErrors, e => e.Contains("Thiếu"));
    }

    [Fact(DisplayName = "TC05-INT-10 - PublishScheduleAsync với kỳ dùng ShiftType cũ (IsActive = false) thiếu định mức -> Thất bại, vẫn là Draft, không công bố")]
    public async Task PublishScheduleAsync_WhenPeriodUsesOldInactiveShiftsAndUnderstaffed_FailsAndRemainsDraft()
    {
        var period = CreateStandardPeriod();
        _periodRepo.Add(period);

        // Version 1 (cũ): IsActive = false
        var oldMorning = CreateShiftType(1, "MORNING", "Ca Sáng (cũ)", 8, 12);
        oldMorning.IsActive = false;
        var oldAfternoon = CreateShiftType(2, "AFTERNOON", "Ca Chiều (cũ)", 12, 16);
        oldAfternoon.IsActive = false;
        var oldEvening = CreateShiftType(3, "EVENING", "Ca Tối (cũ)", 16, 20);
        oldEvening.IsActive = false;

        // Version 2 (mới): IsActive = true
        var newMorning = CreateShiftType(4, "MORNING", "Ca Sáng (mới)", 7, 11);
        newMorning.IsActive = true;
        var newAfternoon = CreateShiftType(5, "AFTERNOON", "Ca Chiều (mới)", 11, 15);
        newAfternoon.IsActive = true;
        var newEvening = CreateShiftType(6, "EVENING", "Ca Tối (mới)", 15, 19);
        newEvening.IsActive = true;

        _shiftTypeRepo.ShiftTypes.AddRange(new[] { oldMorning, oldAfternoon, oldEvening, newMorning, newAfternoon, newEvening });

        // Kỳ dùng ca cũ và thiếu định mức
        period.StaffingRequirements.Add(new StaffingRequirement
        {
            SchedulePeriodId = period.SchedulePeriodId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = oldMorning.ShiftTypeId,
            ShiftType = oldMorning,
            RequiredSalesStaff = 2,
            RequiredCashiers = 1
        });

        var emp1 = CreateEmployee(1, EmploymentType.FullTime, EmployeePosition.Sales);
        _employeeRepo.Add(emp1);

        var draft = await _workScheduleRepo.EnsureDraftExistsAsync(period.SchedulePeriodId, 2, _clock.UtcNow);
        // Chỉ có 1 phân công trong khi cần 2 Sales + 1 Cashier
        draft.ShiftAssignments.Add(new ShiftAssignment
        {
            WorkScheduleId = draft.WorkScheduleId,
            EmployeeId = emp1.EmployeeId,
            WorkDate = period.WeekStartDate,
            ShiftTypeId = oldMorning.ShiftTypeId
        });

        var result = await _scheduleService.PublishScheduleAsync(period.SchedulePeriodId, accountId: 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePublish, result.ErrorCode);
        Assert.Contains("vi phạm bắt buộc", result.ErrorMessage);
        Assert.Equal(ScheduleStatus.Draft, draft.Status);
        Assert.Equal(0, _workScheduleRepo.PublishScheduleCount);
        Assert.Empty(_notificationRepo.Items);
    }
}
