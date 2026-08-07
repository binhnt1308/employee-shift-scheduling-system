using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.ShiftTransfers.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.Fakes;

internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = [];
    public List<int> ManagerAccountIds { get; } = [];

    public void Add(Notification notification)
    {
        Items.Add(notification);
    }

    public Task<IReadOnlyList<int>> GetActiveManagerAccountIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<int>>(ManagerAccountIds);
    }
}

internal sealed class FakeLeaveRequestRepository : ILeaveRequestRepository
{
    public List<Employee> Employees { get; } = [];
    public List<SchedulePeriod> Periods { get; } = [];
    public List<ShiftType> ShiftTypes { get; } = [];
    public List<LeaveRequest> Requests { get; } = [];
    public List<ShiftAssignment> PublishedAssignments { get; } = [];
    public List<ShiftAssignment> DraftAssignments { get; } = [];

    public Task<Employee?> GetEmployeeByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Employees.SingleOrDefault(employee => employee.UserAccountId == accountId));
    }

    public Task<IReadOnlyList<SchedulePeriod>> GetPeriodsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SchedulePeriod>>(Periods);
    }

    public Task<IReadOnlyList<ShiftType>> GetActiveShiftTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftType>>(
            ShiftTypes.Where(shift => shift.IsActive).ToArray());
    }

    public Task<IReadOnlyList<LeaveRequest>> GetForEmployeeAsync(
        int employeeId,
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<LeaveRequest> query = Requests.Where(request => request.EmployeeId == employeeId);
        if (schedulePeriodId.HasValue)
        {
            query = query.Where(request => request.SchedulePeriodId == schedulePeriodId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        return Task.FromResult<IReadOnlyList<LeaveRequest>>(query.ToArray());
    }

    public Task<IReadOnlyList<LeaveRequest>> GetForManagerAsync(
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<LeaveRequest> query = Requests;
        if (schedulePeriodId.HasValue)
        {
            query = query.Where(request => request.SchedulePeriodId == schedulePeriodId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        return Task.FromResult<IReadOnlyList<LeaveRequest>>(query.ToArray());
    }

    public Task<LeaveRequest?> GetByIdAsync(
        int leaveRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Requests.SingleOrDefault(request => request.LeaveRequestId == leaveRequestId));
    }

    public Task<bool> HasEffectiveDuplicateAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var exists = Requests.Any(request => request.EmployeeId == employeeId
            && request.SchedulePeriodId == schedulePeriodId
            && request.LeaveDate == leaveDate
            && request.Status != LeaveRequestStatus.Rejected
            && (!shiftTypeId.HasValue || !request.ShiftTypeId.HasValue || request.ShiftTypeId == shiftTypeId));
        return Task.FromResult(exists);
    }

    public Task<bool> HasPublishedAssignmentAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var exists = PublishedAssignments.Any(assignment => assignment.EmployeeId == employeeId
            && assignment.WorkDate == leaveDate
            && assignment.WorkSchedule.SchedulePeriodId == schedulePeriodId
            && assignment.WorkSchedule.Status == ScheduleStatus.Published
            && (!shiftTypeId.HasValue || assignment.ShiftTypeId == shiftTypeId.Value));
        return Task.FromResult(exists);
    }

    public Task<bool> HasDraftAssignmentAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var exists = DraftAssignments.Any(assignment => assignment.EmployeeId == employeeId
            && assignment.WorkDate == leaveDate
            && assignment.WorkSchedule.SchedulePeriodId == schedulePeriodId
            && assignment.WorkSchedule.Status == ScheduleStatus.Draft
            && (!shiftTypeId.HasValue || assignment.ShiftTypeId == shiftTypeId.Value));
        return Task.FromResult(exists);
    }

    public void Add(LeaveRequest leaveRequest)
    {
        leaveRequest.LeaveRequestId = leaveRequest.LeaveRequestId == 0
            ? Requests.Count + 1
            : leaveRequest.LeaveRequestId;
        WireLeaveRequest(leaveRequest);
        Requests.Add(leaveRequest);
    }

    public void WireLeaveRequest(LeaveRequest request)
    {
        request.Employee = Employees.Single(employee => employee.EmployeeId == request.EmployeeId);
        request.SchedulePeriod = Periods.Single(period => period.SchedulePeriodId == request.SchedulePeriodId);
        request.ShiftType = request.ShiftTypeId.HasValue
            ? ShiftTypes.Single(shift => shift.ShiftTypeId == request.ShiftTypeId.Value)
            : null;
    }
}

internal sealed class FakeShiftTransferRepository : IShiftTransferRepository
{
    public List<Employee> Employees { get; } = [];
    public List<ShiftAssignment> Assignments { get; } = [];
    public List<Availability> Availabilities { get; } = [];
    public List<LeaveRequest> LeaveRequests { get; } = [];
    public List<StaffingRequirement> Requirements { get; } = [];
    public List<ShiftTransferRequest> Requests { get; } = [];

    public Task<Employee?> GetEmployeeByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Employees.SingleOrDefault(employee => employee.UserAccountId == accountId));
    }

    public Task<IReadOnlyList<ShiftAssignment>> GetPublishedAssignmentsForEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftAssignment>>(
            Assignments.Where(assignment => assignment.EmployeeId == employeeId
                && assignment.WorkSchedule.Status == ScheduleStatus.Published).ToArray());
    }

    public Task<ShiftAssignment?> GetAssignmentByIdAsync(
        int shiftAssignmentId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Assignments.SingleOrDefault(assignment => assignment.ShiftAssignmentId == shiftAssignmentId));
    }

    public Task<IReadOnlyList<Employee>> GetActiveEmployeesByPositionAsync(
        EmployeePosition position,
        int excludedEmployeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Employee>>(Employees
            .Where(employee => employee.IsActive
                && employee.Position == position
                && employee.EmployeeId != excludedEmployeeId)
            .ToArray());
    }

    public Task<bool> HasAvailabilityAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Availabilities.Any(availability => availability.EmployeeId == employeeId
            && availability.SchedulePeriodId == schedulePeriodId
            && availability.WorkDate == workDate
            && availability.ShiftTypeId == shiftTypeId));
    }

    public Task<bool> HasApprovedLeaveAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LeaveRequests.Any(request => request.EmployeeId == employeeId
            && request.SchedulePeriodId == schedulePeriodId
            && request.LeaveDate == workDate
            && request.Status == LeaveRequestStatus.Approved
            && (!request.ShiftTypeId.HasValue || request.ShiftTypeId == shiftTypeId)));
    }

    public Task<IReadOnlyList<ShiftAssignment>> GetPublishedAssignmentsForEmployeeInPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftAssignment>>(Assignments
            .Where(assignment => assignment.EmployeeId == employeeId
                && assignment.WorkSchedule.Status == ScheduleStatus.Published
                && assignment.WorkSchedule.SchedulePeriodId == schedulePeriodId)
            .ToArray());
    }

    public Task<IReadOnlyList<ShiftAssignment>> GetAssignmentsForShiftAsync(
        int workScheduleId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftAssignment>>(Assignments
            .Where(assignment => assignment.WorkScheduleId == workScheduleId
                && assignment.WorkDate == workDate
                && assignment.ShiftTypeId == shiftTypeId)
            .ToArray());
    }

    public Task<StaffingRequirement?> GetStaffingRequirementAsync(
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Requirements.SingleOrDefault(requirement =>
            requirement.SchedulePeriodId == schedulePeriodId
            && requirement.WorkDate == workDate
            && requirement.ShiftTypeId == shiftTypeId));
    }

    public Task<bool> HasActiveRequestForAssignmentAsync(
        int shiftAssignmentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Requests.Any(request => request.ShiftAssignmentId == shiftAssignmentId
            && request.ReceiverStatus != ReceiverStatus.Rejected
            && request.ApprovalStatus == ApprovalStatus.Pending));
    }

    public Task<IReadOnlyList<ShiftTransferRequest>> GetSentRequestsAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftTransferRequest>>(
            Requests.Where(request => request.SenderEmployeeId == employeeId).ToArray());
    }

    public Task<IReadOnlyList<ShiftTransferRequest>> GetReceivedRequestsAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftTransferRequest>>(
            Requests.Where(request => request.ReceiverEmployeeId == employeeId).ToArray());
    }

    public Task<IReadOnlyList<ShiftTransferRequest>> GetManagerRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftTransferRequest>>(Requests.ToArray());
    }

    public Task<ShiftTransferRequest?> GetRequestByIdAsync(
        int shiftTransferRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Requests.SingleOrDefault(request => request.ShiftTransferRequestId == shiftTransferRequestId));
    }

    public void Add(ShiftTransferRequest request)
    {
        request.ShiftTransferRequestId = request.ShiftTransferRequestId == 0
            ? Requests.Count + 1
            : request.ShiftTransferRequestId;
        WireRequest(request);
        Requests.Add(request);
    }

    public void WireRequest(ShiftTransferRequest request)
    {
        request.ShiftAssignment = Assignments.Single(
            assignment => assignment.ShiftAssignmentId == request.ShiftAssignmentId);
        request.SenderEmployee = Employees.Single(employee => employee.EmployeeId == request.SenderEmployeeId);
        request.ReceiverEmployee = Employees.Single(employee => employee.EmployeeId == request.ReceiverEmployeeId);
    }
}
