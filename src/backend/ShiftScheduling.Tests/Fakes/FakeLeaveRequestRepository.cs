using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Tests.Fakes;

// Repository giả lập thông báo lưu dữ liệu trong bộ nhớ, giúp kiểm thử luồng thông báo mà không cần cơ sở dữ liệu thật.
internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = [];
    public List<int> ManagerAccountIds { get; } = [];
    public List<int> EmployeeAccountIds { get; } = [];
    public int GetUnreadCountCallCount { get; private set; }

    public void Add(Notification notification)
    {
        if (notification.NotificationId == 0)
        {
            notification.NotificationId = Items.Count == 0
                ? 1
                : Items.Max(item => item.NotificationId) + 1;
        }

        Items.Add(notification);
    }

    public bool ThrowOnGetActiveManagers { get; set; }

    public Task<IReadOnlyList<int>> GetActiveManagerAccountIdsAsync(
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetActiveManagers)
        {
            throw new InvalidOperationException("Simulated database failure during manager lookup.");
        }

        return Task.FromResult<IReadOnlyList<int>>(ManagerAccountIds);
    }

    public Task<IReadOnlyList<int>> GetActiveEmployeeAccountIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<int>>(EmployeeAccountIds);
    }

    public Task<PagedResult<Notification>> GetPagedByReceiverAccountIdAsync(
        int receiverAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filtered = Items
            .Where(n => n.ReceiverAccountId == receiverAccountId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .ToList();

        var totalItems = filtered.Count;
        var pagedItems = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<Notification>(pagedItems, page, pageSize, totalItems));
    }

    public Task<int> GetUnreadCountByReceiverAccountIdAsync(
        int receiverAccountId,
        CancellationToken cancellationToken = default)
    {
        GetUnreadCountCallCount++;
        var count = Items.Count(n => n.ReceiverAccountId == receiverAccountId && !n.IsRead);
        return Task.FromResult(count);
    }

    public Task<Notification?> GetByIdAndReceiverAccountIdAsync(
        int notificationId,
        int receiverAccountId,
        CancellationToken cancellationToken = default)
    {
        var item = Items.FirstOrDefault(
            n => n.NotificationId == notificationId && n.ReceiverAccountId == receiverAccountId);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<Notification>> GetUnreadByReceiverAccountIdAsync(
        int receiverAccountId,
        CancellationToken cancellationToken = default)
    {
        var unread = Items
            .Where(n => n.ReceiverAccountId == receiverAccountId && !n.IsRead)
            .ToList();
        return Task.FromResult<IReadOnlyList<Notification>>(unread);
    }
}

// Repository giả lập đơn nghỉ mô phỏng các truy vấn và bộ lọc quan trọng,
// giúp kiểm thử nghiệp vụ nghỉ phép độc lập với EF Core.
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
            && request.Status != LeaveRequestStatus.Cancelled
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

    public Task<IReadOnlyList<ShiftAssignment>> GetEmployeePublishedAssignmentsAsync(
        int employeeId,
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        var items = PublishedAssignments
            .Where(a => a.EmployeeId == employeeId
                && a.WorkSchedule.Status == ScheduleStatus.Published
                && a.WorkSchedule.SchedulePeriod.WeekEndDate >= fromDate)
            .OrderBy(a => a.WorkDate)
            .ToList();
        return Task.FromResult<IReadOnlyList<ShiftAssignment>>(items);
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
            ? ShiftTypes.SingleOrDefault(shift => shift.ShiftTypeId == request.ShiftTypeId.Value)
            : null;
    }
}
