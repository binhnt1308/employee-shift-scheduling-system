using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTransfers.Repositories;

/// <summary>
/// Truy cập dữ liệu chuyển ca. Các truy vấn workflow nạp đủ assignment, kỳ lịch và nhân viên cần thiết
/// để Service có thể kiểm tra hard rule trong bộ nhớ mà không phát sinh N+1.
/// </summary>
public sealed class ShiftTransferRepository : IShiftTransferRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ShiftTransferRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Employee?> GetEmployeeByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Employee> query = _dbContext.Employees.Include(employee => employee.UserAccount);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(employee => employee.UserAccountId == accountId, cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftAssignment>> GetPublishedAssignmentsForEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return await AssignmentQuery(true)
            .Where(assignment => assignment.EmployeeId == employeeId
                && assignment.WorkSchedule.Status == ScheduleStatus.Published)
            .OrderBy(assignment => assignment.WorkDate)
            .ThenBy(assignment => assignment.ShiftType.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public Task<ShiftAssignment?> GetAssignmentByIdAsync(
        int shiftAssignmentId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return AssignmentQuery(asNoTracking)
            .SingleOrDefaultAsync(
                assignment => assignment.ShiftAssignmentId == shiftAssignmentId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Employee>> GetActiveEmployeesByPositionAsync(
        EmployeePosition position,
        int excludedEmployeeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .Include(employee => employee.UserAccount)
            .Where(employee => employee.IsActive
                && employee.Position == position
                && employee.EmployeeId != excludedEmployeeId)
            .OrderBy(employee => employee.FullName)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasAvailabilityAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Availabilities.AsNoTracking().AnyAsync(
            availability => availability.EmployeeId == employeeId
                && availability.SchedulePeriodId == schedulePeriodId
                && availability.WorkDate == workDate
                && availability.ShiftTypeId == shiftTypeId,
            cancellationToken);
    }

    public Task<bool> HasApprovedLeaveAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LeaveRequests.AsNoTracking().AnyAsync(
            request => request.EmployeeId == employeeId
                && request.SchedulePeriodId == schedulePeriodId
                && request.LeaveDate == workDate
                && request.Status == LeaveRequestStatus.Approved
                && (!request.ShiftTypeId.HasValue || request.ShiftTypeId == shiftTypeId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftAssignment>> GetPublishedAssignmentsForEmployeeInPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return await AssignmentQuery(true)
            .Where(assignment => assignment.EmployeeId == employeeId
                && assignment.WorkSchedule.Status == ScheduleStatus.Published
                && assignment.WorkSchedule.SchedulePeriodId == schedulePeriodId)
            .OrderBy(assignment => assignment.WorkDate)
            .ThenBy(assignment => assignment.ShiftType.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftAssignment>> GetAssignmentsForShiftAsync(
        int workScheduleId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return await AssignmentQuery(true)
            .Where(assignment => assignment.WorkScheduleId == workScheduleId
                && assignment.WorkDate == workDate
                && assignment.ShiftTypeId == shiftTypeId)
            .ToListAsync(cancellationToken);
    }

    public Task<StaffingRequirement?> GetStaffingRequirementAsync(
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.StaffingRequirements.AsNoTracking().SingleOrDefaultAsync(
            requirement => requirement.SchedulePeriodId == schedulePeriodId
                && requirement.WorkDate == workDate
                && requirement.ShiftTypeId == shiftTypeId,
            cancellationToken);
    }

    public Task<bool> HasActiveRequestForAssignmentAsync(
        int shiftAssignmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShiftTransferRequests.AsNoTracking().AnyAsync(
            request => request.ShiftAssignmentId == shiftAssignmentId
                && request.ReceiverStatus != ReceiverStatus.Rejected
                && request.ApprovalStatus == ApprovalStatus.Pending,
            cancellationToken);
    }

    public async Task<HashSet<int>> GetCandidateAvailabilitiesAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var ids = candidateEmployeeIds.ToList();
        var set = await _dbContext.Availabilities
            .AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId)
                && a.SchedulePeriodId == schedulePeriodId
                && a.WorkDate == workDate
                && a.ShiftTypeId == shiftTypeId)
            .Select(a => a.EmployeeId)
            .ToListAsync(cancellationToken);
        return set.ToHashSet();
    }

    public async Task<HashSet<int>> GetCandidateApprovedLeavesAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var ids = candidateEmployeeIds.ToList();
        var set = await _dbContext.LeaveRequests
            .AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId)
                && r.SchedulePeriodId == schedulePeriodId
                && r.LeaveDate == workDate
                && r.Status == LeaveRequestStatus.Approved
                && (!r.ShiftTypeId.HasValue || r.ShiftTypeId == shiftTypeId))
            .Select(r => r.EmployeeId)
            .ToListAsync(cancellationToken);
        return set.ToHashSet();
    }

    public async Task<ILookup<int, ShiftAssignment>> GetCandidatePublishedAssignmentsInPeriodAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var ids = candidateEmployeeIds.ToList();
        var assignments = await AssignmentQuery(true)
            .Where(a => ids.Contains(a.EmployeeId)
                && a.WorkSchedule.Status == ScheduleStatus.Published
                && a.WorkSchedule.SchedulePeriodId == schedulePeriodId)
            .OrderBy(a => a.WorkDate)
            .ThenBy(a => a.ShiftType.DisplayOrder)
            .ToListAsync(cancellationToken);
        return assignments.ToLookup(a => a.EmployeeId);
    }

    public async Task<IReadOnlyList<ShiftTransferRequest>> GetSentRequestsAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return await RequestQuery(true)
            .Where(request => request.SenderEmployeeId == employeeId)
            .OrderByDescending(request => request.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ShiftTransferRequest>> GetPagedSentRequestsAsync(
        int employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ListQuery(true).Where(request => request.SenderEmployeeId == employeeId);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(request => request.SubmittedAt)
            .ThenBy(request => request.ShiftTransferRequestId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ShiftTransferRequest>(items, page, pageSize, totalItems);
    }

    public async Task<IReadOnlyList<ShiftTransferRequest>> GetReceivedRequestsAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return await RequestQuery(true)
            .Where(request => request.ReceiverEmployeeId == employeeId)
            .OrderBy(request => request.ReceiverStatus)
            .ThenByDescending(request => request.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ShiftTransferRequest>> GetPagedReceivedRequestsAsync(
        int employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ListQuery(true).Where(request => request.ReceiverEmployeeId == employeeId);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(request => request.ReceiverStatus)
            .ThenByDescending(request => request.SubmittedAt)
            .ThenBy(request => request.ShiftTransferRequestId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ShiftTransferRequest>(items, page, pageSize, totalItems);
    }

    public async Task<IReadOnlyList<ShiftTransferRequest>> GetManagerRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return await RequestQuery(true)
            .OrderBy(request => request.ApprovalStatus)
            .ThenBy(request => request.ReceiverStatus)
            .ThenByDescending(request => request.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ShiftTransferRequest>> GetPagedManagerRequestsAsync(
        ShiftTransferStatusFilter? filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ListQuery(true);
        if (filter.HasValue)
        {
            query = filter.Value switch
            {
                ShiftTransferStatusFilter.WaitingReceiver => query.Where(r =>
                    r.ReceiverStatus == ReceiverStatus.Pending),
                ShiftTransferStatusFilter.WaitingManager => query.Where(r =>
                    r.ReceiverStatus == ReceiverStatus.Accepted && r.ApprovalStatus == ApprovalStatus.Pending),
                ShiftTransferStatusFilter.Approved => query.Where(r => r.ApprovalStatus == ApprovalStatus.Approved),
                ShiftTransferStatusFilter.Rejected => query.Where(r =>
                    r.ReceiverStatus == ReceiverStatus.Rejected || r.ApprovalStatus == ApprovalStatus.Rejected),
                _ => query
            };
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(request => request.ApprovalStatus)
            .ThenBy(request => request.ReceiverStatus)
            .ThenByDescending(request => request.SubmittedAt)
            .ThenBy(request => request.ShiftTransferRequestId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ShiftTransferRequest>(items, page, pageSize, totalItems);
    }

    public Task<ShiftTransferRequest?> GetRequestByIdAsync(
        int shiftTransferRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return RequestQuery(asNoTracking)
            .SingleOrDefaultAsync(
                request => request.ShiftTransferRequestId == shiftTransferRequestId,
                cancellationToken);
    }

    public void Add(ShiftTransferRequest request)
    {
        _dbContext.ShiftTransferRequests.Add(request);
    }

    public void SetOriginalRowVersion(ShiftTransferRequest request, byte[] rowVersion)
    {
        _dbContext.Entry(request).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }

    private IQueryable<ShiftAssignment> AssignmentQuery(bool asNoTracking)
    {
        IQueryable<ShiftAssignment> query = _dbContext.ShiftAssignments
            .Include(assignment => assignment.Employee)
                .ThenInclude(employee => employee.UserAccount)
            .Include(assignment => assignment.ShiftType)
            .Include(assignment => assignment.WorkSchedule)
                .ThenInclude(schedule => schedule.SchedulePeriod)
                    .ThenInclude(period => period.StaffingRequirements)
                        .ThenInclude(requirement => requirement.ShiftType)
            .AsSplitQuery();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<ShiftTransferRequest> RequestQuery(bool asNoTracking)
    {
        IQueryable<ShiftTransferRequest> query = _dbContext.ShiftTransferRequests
            .Include(request => request.SenderEmployee)
                .ThenInclude(employee => employee.UserAccount)
            .Include(request => request.ReceiverEmployee)
                .ThenInclude(employee => employee.UserAccount)
            .Include(request => request.ShiftAssignment)
                .ThenInclude(assignment => assignment.ShiftType)
            .Include(request => request.ShiftAssignment)
                .ThenInclude(assignment => assignment.WorkSchedule)
                    .ThenInclude(schedule => schedule.SchedulePeriod)
                        .ThenInclude(period => period.StaffingRequirements)
                            .ThenInclude(requirement => requirement.ShiftType)
            .Include(request => request.ApprovedByAccount)
            .AsSplitQuery();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<ShiftTransferRequest> ListQuery(bool asNoTracking)
    {
        IQueryable<ShiftTransferRequest> query = _dbContext.ShiftTransferRequests
            .Include(request => request.SenderEmployee)
            .Include(request => request.ReceiverEmployee)
            .Include(request => request.ShiftAssignment)
                .ThenInclude(assignment => assignment.ShiftType)
            .Include(request => request.ShiftAssignment)
                .ThenInclude(assignment => assignment.WorkSchedule)
                    .ThenInclude(schedule => schedule.SchedulePeriod);
        return asNoTracking ? query.AsNoTracking() : query;
    }
}
