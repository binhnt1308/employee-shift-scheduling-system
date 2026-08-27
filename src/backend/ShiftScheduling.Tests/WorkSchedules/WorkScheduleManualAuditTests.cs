using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class WorkScheduleManualAuditTests
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

    public WorkScheduleManualAuditTests()
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

    [Fact(DisplayName = "TC05-LG04-01 - Removal succeeds and writes complete audit data")]
    public async Task RemoveEmployeeManualAsync_WhenAssignmentExists_AddsAuditLogAndSaves()
    {
        const int schedulePeriodId = 1;
        const int workScheduleId = 10;
        const int assignmentId = 101;
        const int employeeId = 5;
        const int shiftTypeId = 1;
        const int actorAccountId = 99;
        var workDate = new DateOnly(2026, 8, 10);

        var assignment = CreateAssignment(assignmentId, workScheduleId, employeeId, workDate, shiftTypeId);
        AddSchedule(workScheduleId, schedulePeriodId, ScheduleStatus.Draft, [assignment]);
        var model = CreateRemoveModel(schedulePeriodId, assignmentId);

        var result = await _scheduleService.RemoveEmployeeManualAsync(model, actorAccountId);

        Assert.True(result.Succeeded);
        Assert.Equal(1, _workScheduleRepo.SaveAssignmentsCount);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Single(_auditLogRepo.Items);

        var log = _auditLogRepo.Items[0];
        Assert.Equal("MANUAL_REMOVE_EMPLOYEE", log.Action);
        Assert.Equal(nameof(ShiftAssignment), log.EntityName);
        Assert.Equal(assignmentId.ToString(), log.EntityId);
        Assert.Equal(actorAccountId, log.ActorAccountId);
        Assert.Null(log.NewValues);
        Assert.Equal(_clock.UtcNow, log.CreatedAtUtc);
        AssertAuditPreviousValues(log.PreviousValues, schedulePeriodId, workScheduleId, assignment);
    }

    [Fact(DisplayName = "TC05-LG04-02 - Missing assignment writes no audit and saves nothing")]
    public async Task RemoveEmployeeManualAsync_WhenAssignmentDoesNotExist_DoesNotAddAuditLogOrSave()
    {
        const int schedulePeriodId = 1;
        const int workScheduleId = 10;
        const int actorAccountId = 99;

        AddSchedule(workScheduleId, schedulePeriodId, ScheduleStatus.Draft);
        var model = CreateRemoveModel(schedulePeriodId, 999);

        var result = await _scheduleService.RemoveEmployeeManualAsync(model, actorAccountId);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftAssignmentNotFound, result.ErrorCode);
        AssertNoPersistenceOccurred();
    }

    [Fact(DisplayName = "TC05-LG04-03 - Published schedule writes no audit and saves nothing")]
    public async Task RemoveEmployeeManualAsync_WhenScheduleIsPublished_DoesNotAddAuditLogOrSave()
    {
        const int schedulePeriodId = 1;
        const int workScheduleId = 10;
        const int actorAccountId = 99;

        AddSchedule(workScheduleId, schedulePeriodId, ScheduleStatus.Published);
        var model = CreateRemoveModel(schedulePeriodId, 101);

        var result = await _scheduleService.RemoveEmployeeManualAsync(model, actorAccountId);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidScheduleStatus, result.ErrorCode);
        AssertNoPersistenceOccurred();
    }

    private void AddSchedule(
        int workScheduleId,
        int schedulePeriodId,
        ScheduleStatus status,
        List<ShiftAssignment>? assignments = null)
    {
        _workScheduleRepo.Schedules.Add(new WorkSchedule
        {
            WorkScheduleId = workScheduleId,
            SchedulePeriodId = schedulePeriodId,
            Status = status,
            ShiftAssignments = assignments ?? []
        });
    }

    private static ShiftAssignment CreateAssignment(
        int assignmentId,
        int workScheduleId,
        int employeeId,
        DateOnly workDate,
        int shiftTypeId) => new()
    {
        ShiftAssignmentId = assignmentId,
        WorkScheduleId = workScheduleId,
        EmployeeId = employeeId,
        WorkDate = workDate,
        ShiftTypeId = shiftTypeId,
        Source = AssignmentSource.Manual
    };

    private static ManualRemoveRequestViewModel CreateRemoveModel(int schedulePeriodId, int assignmentId) => new()
    {
        SchedulePeriodId = schedulePeriodId,
        ShiftAssignmentId = assignmentId
    };

    private void AssertNoPersistenceOccurred()
    {
        Assert.Empty(_auditLogRepo.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Equal(0, _workScheduleRepo.SaveAssignmentsCount);
    }

    private static void AssertAuditPreviousValues(
        string? previousValuesJson,
        int expectedPeriodId,
        int expectedScheduleId,
        ShiftAssignment expectedAssignment)
    {
        Assert.NotNull(previousValuesJson);
        using var doc = JsonDocument.Parse(previousValuesJson!);
        var root = doc.RootElement;
        Assert.Equal(expectedPeriodId, root.GetProperty("SchedulePeriodId").GetInt32());
        Assert.Equal(expectedScheduleId, root.GetProperty("WorkScheduleId").GetInt32());
        Assert.Equal(expectedAssignment.ShiftAssignmentId, root.GetProperty("ShiftAssignmentId").GetInt32());
        Assert.Equal(expectedAssignment.EmployeeId, root.GetProperty("EmployeeId").GetInt32());
        Assert.Equal(expectedAssignment.WorkDate.ToString("yyyy-MM-dd"), root.GetProperty("WorkDate").GetString());
        Assert.Equal(expectedAssignment.ShiftTypeId, root.GetProperty("ShiftTypeId").GetInt32());
        Assert.Equal((int)expectedAssignment.Source, root.GetProperty("Source").GetInt32());
    }
}
