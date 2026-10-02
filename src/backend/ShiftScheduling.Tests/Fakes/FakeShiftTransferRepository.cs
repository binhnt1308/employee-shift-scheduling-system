using ShiftScheduling.Features.ShiftTransfers.Repositories;
using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Tests.Fakes;

// Fake repository chuyển ca phục vụ unit test các rule mà không cần cơ sở dữ liệu thật.
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
        return Task.FromResult(
            Employees.SingleOrDefault(employee => employee.UserAccountId == accountId));
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
            Assignments.SingleOrDefault(
                assignment => assignment.ShiftAssignmentId == shiftAssignmentId));
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
        return Task.FromResult(Availabilities.Any(availability =>
            availability.EmployeeId == employeeId
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
        return Task.FromResult(LeaveRequests.Any(request =>
            request.EmployeeId == employeeId
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
        return Task.FromResult(Requests.Any(request =>
            request.ShiftAssignmentId == shiftAssignmentId
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

    public Task<PagedResult<ShiftTransferRequest>> GetPagedSentRequestsAsync(
        int employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var list = Requests.Where(request => request.SenderEmployeeId == employeeId).ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(
            new PagedResult<ShiftTransferRequest>(items, page, pageSize, list.Count));
    }

    public Task<IReadOnlyList<ShiftTransferRequest>> GetReceivedRequestsAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftTransferRequest>>(
            Requests.Where(request => request.ReceiverEmployeeId == employeeId).ToArray());
    }

    public Task<PagedResult<ShiftTransferRequest>> GetPagedReceivedRequestsAsync(
        int employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var list = Requests
            .Where(request => request.ReceiverEmployeeId == employeeId).ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(
            new PagedResult<ShiftTransferRequest>(items, page, pageSize, list.Count));
    }

    public Task<IReadOnlyList<ShiftTransferRequest>> GetManagerRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ShiftTransferRequest>>(Requests.ToArray());
    }

    public Task<PagedResult<ShiftTransferRequest>> GetPagedManagerRequestsAsync(
        ShiftTransferStatusFilter? filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ShiftTransferRequest> query = Requests;
        if (filter.HasValue)
        {
            query = filter.Value switch
            {
                ShiftTransferStatusFilter.WaitingReceiver =>
                    query.Where(r => r.ReceiverStatus == ReceiverStatus.Pending),
                ShiftTransferStatusFilter.WaitingManager =>
                    query.Where(r =>
                        r.ReceiverStatus == ReceiverStatus.Accepted
                        && r.ApprovalStatus == ApprovalStatus.Pending),
                ShiftTransferStatusFilter.Approved =>
                    query.Where(r => r.ApprovalStatus == ApprovalStatus.Approved),
                ShiftTransferStatusFilter.Rejected =>
                    query.Where(r =>
                        r.ReceiverStatus == ReceiverStatus.Rejected
                        || r.ApprovalStatus == ApprovalStatus.Rejected),
                _ => query
            };
        }

        var list = query.ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(
            new PagedResult<ShiftTransferRequest>(items, page, pageSize, list.Count));
    }

    public Task<ShiftTransferRequest?> GetRequestByIdAsync(
        int shiftTransferRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Requests.SingleOrDefault(
                request => request.ShiftTransferRequestId == shiftTransferRequestId));
    }

    public Task<HashSet<int>> GetCandidateAvailabilitiesAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var ids = candidateEmployeeIds.ToHashSet();
        var set = Availabilities
            .Where(a => ids.Contains(a.EmployeeId)
                && a.SchedulePeriodId == schedulePeriodId
                && a.WorkDate == workDate
                && a.ShiftTypeId == shiftTypeId)
            .Select(a => a.EmployeeId)
            .ToHashSet();
        return Task.FromResult(set);
    }

    public Task<HashSet<int>> GetCandidateApprovedLeavesAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var ids = candidateEmployeeIds.ToHashSet();
        var set = LeaveRequests
            .Where(r => ids.Contains(r.EmployeeId)
                && r.SchedulePeriodId == schedulePeriodId
                && r.LeaveDate == workDate
                && r.Status == LeaveRequestStatus.Approved
                && (!r.ShiftTypeId.HasValue || r.ShiftTypeId == shiftTypeId))
            .Select(r => r.EmployeeId)
            .ToHashSet();
        return Task.FromResult(set);
    }

    public Task<ILookup<int, ShiftAssignment>> GetCandidatePublishedAssignmentsInPeriodAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var ids = candidateEmployeeIds.ToHashSet();
        var lookup = Assignments
            .Where(a => ids.Contains(a.EmployeeId)
                && a.WorkSchedule.Status == ScheduleStatus.Published
                && a.WorkSchedule.SchedulePeriodId == schedulePeriodId)
            .ToLookup(a => a.EmployeeId);
        return Task.FromResult(lookup);
    }

    public void Add(ShiftTransferRequest request)
    {
        request.ShiftTransferRequestId = request.ShiftTransferRequestId == 0
            ? Requests.Count + 1
            : request.ShiftTransferRequestId;
        WireRequest(request);
        Requests.Add(request);
    }

    public byte[]? TargetOriginalRowVersion { get; private set; }

    public void SetOriginalRowVersion(ShiftTransferRequest request, byte[] rowVersion)
    {
        TargetOriginalRowVersion = rowVersion;
    }

    public void WireRequest(ShiftTransferRequest request)
    {
        request.ShiftAssignment = Assignments.Single(
            assignment => assignment.ShiftAssignmentId == request.ShiftAssignmentId);
        request.SenderEmployee =
            Employees.Single(employee => employee.EmployeeId == request.SenderEmployeeId);
        request.ReceiverEmployee =
            Employees.Single(employee => employee.EmployeeId == request.ReceiverEmployeeId);
    }
}
