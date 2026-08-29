using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

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
        return Task.FromResult(
            Employees.SingleOrDefault(employee => employee.UserAccountId == accountId));
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
        IEnumerable<LeaveRequest> query =
            Requests.Where(request => request.EmployeeId == employeeId);
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

    public Task<PagedResult<LeaveRequest>> GetPagedForEmployeeAsync(
        int employeeId,
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<LeaveRequest> query =
            Requests.Where(request => request.EmployeeId == employeeId);
        if (schedulePeriodId.HasValue)
        {
            query = query.Where(request => request.SchedulePeriodId == schedulePeriodId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        var list = query.ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<LeaveRequest>(items, page, pageSize, list.Count));
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

    public Task<PagedResult<LeaveRequest>> GetPagedForManagerAsync(
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        int page,
        int pageSize,
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

        var list = query.ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<LeaveRequest>(items, page, pageSize, list.Count));
    }

    public Task<LeaveRequest?> GetByIdAsync(
        int leaveRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Requests.SingleOrDefault(request => request.LeaveRequestId == leaveRequestId));
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
            && (!shiftTypeId.HasValue
                || !request.ShiftTypeId.HasValue
                || request.ShiftTypeId == shiftTypeId));
        return Task.FromResult(exists);
    }

    public Task<bool> HasPublishedAssignmentAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var exists = PublishedAssignments.Any(assignment =>
            assignment.EmployeeId == employeeId
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
        var exists = DraftAssignments.Any(assignment =>
            assignment.EmployeeId == employeeId
            && assignment.WorkDate == leaveDate
            && assignment.WorkSchedule.SchedulePeriodId == schedulePeriodId
            && assignment.WorkSchedule.Status == ScheduleStatus.Draft
            && (!shiftTypeId.HasValue || assignment.ShiftTypeId == shiftTypeId.Value));
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<LeaveRequest>> GetApprovedLeavesByPeriodAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<LeaveRequest>>(
            Requests.Where(r =>
                r.SchedulePeriodId == schedulePeriodId
                && r.Status == LeaveRequestStatus.Approved).ToList());
    }

    public void Add(LeaveRequest leaveRequest)
    {
        leaveRequest.LeaveRequestId = leaveRequest.LeaveRequestId == 0
            ? Requests.Count + 1
            : leaveRequest.LeaveRequestId;
        WireLeaveRequest(leaveRequest);
        Requests.Add(leaveRequest);
    }

    public byte[]? TargetOriginalRowVersion { get; private set; }

    public void SetOriginalRowVersion(LeaveRequest request, byte[] rowVersion)
    {
        TargetOriginalRowVersion = rowVersion;
    }

    public void WireLeaveRequest(LeaveRequest request)
    {
        request.Employee = Employees.Single(employee => employee.EmployeeId == request.EmployeeId);
        request.SchedulePeriod =
            Periods.Single(period => period.SchedulePeriodId == request.SchedulePeriodId);
        request.ShiftType = request.ShiftTypeId.HasValue
            ? ShiftTypes.Single(shift => shift.ShiftTypeId == request.ShiftTypeId.Value)
            : null;
    }
}
