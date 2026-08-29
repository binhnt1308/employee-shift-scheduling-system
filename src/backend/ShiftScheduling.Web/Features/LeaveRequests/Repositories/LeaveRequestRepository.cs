using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.LeaveRequests.Repositories;

public sealed class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly ApplicationDbContext _dbContext;

    public LeaveRequestRepository(ApplicationDbContext dbContext)
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

    public async Task<IReadOnlyList<SchedulePeriod>> GetPeriodsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SchedulePeriods
            .AsNoTracking()
            .OrderByDescending(period => period.WeekStartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftType>> GetActiveShiftTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShiftTypes
            .AsNoTracking()
            .Where(shift => shift.IsActive)
            .OrderBy(shift => shift.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetForEmployeeAsync(
        int employeeId,
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery(true).Where(request => request.EmployeeId == employeeId);
        if (schedulePeriodId.HasValue)
        {
            query = query.Where(request => request.SchedulePeriodId == schedulePeriodId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        return await query
            .OrderByDescending(request => request.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<LeaveRequest>> GetPagedForEmployeeAsync(
        int employeeId,
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery(true).Where(request => request.EmployeeId == employeeId);
        if (schedulePeriodId.HasValue)
        {
            query = query.Where(request => request.SchedulePeriodId == schedulePeriodId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(request => request.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LeaveRequest>(items, page, pageSize, totalItems);
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetForManagerAsync(
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery(true);
        if (schedulePeriodId.HasValue)
        {
            query = query.Where(request => request.SchedulePeriodId == schedulePeriodId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        return await query
            .OrderBy(request => request.Status)
            .ThenByDescending(request => request.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<LeaveRequest>> GetPagedForManagerAsync(
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery(true);
        if (schedulePeriodId.HasValue)
        {
            query = query.Where(request => request.SchedulePeriodId == schedulePeriodId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(request => request.Status)
            .ThenByDescending(request => request.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LeaveRequest>(items, page, pageSize, totalItems);
    }

    public Task<LeaveRequest?> GetByIdAsync(
        int leaveRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default)
    {
        return BaseQuery(asNoTracking)
            .SingleOrDefaultAsync(request => request.LeaveRequestId == leaveRequestId, cancellationToken);
    }

    public Task<bool> HasEffectiveDuplicateAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LeaveRequests.AsNoTracking().AnyAsync(
            request => request.EmployeeId == employeeId
                && request.SchedulePeriodId == schedulePeriodId
                && request.LeaveDate == leaveDate
                && request.Status != LeaveRequestStatus.Rejected
                && (!shiftTypeId.HasValue
                    || !request.ShiftTypeId.HasValue
                    || request.ShiftTypeId == shiftTypeId),
            cancellationToken);
    }

    public Task<bool> HasPublishedAssignmentAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShiftAssignments.AsNoTracking().AnyAsync(
            assignment => assignment.EmployeeId == employeeId
                && assignment.WorkDate == leaveDate
                && assignment.WorkSchedule.SchedulePeriodId == schedulePeriodId
                && assignment.WorkSchedule.Status == ScheduleStatus.Published
                && (!shiftTypeId.HasValue || assignment.ShiftTypeId == shiftTypeId.Value),
            cancellationToken);
    }

    public Task<bool> HasDraftAssignmentAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ShiftAssignments.AsNoTracking().AnyAsync(
            assignment => assignment.EmployeeId == employeeId
                && assignment.WorkDate == leaveDate
                && assignment.WorkSchedule.SchedulePeriodId == schedulePeriodId
                && assignment.WorkSchedule.Status == ScheduleStatus.Draft
                && (!shiftTypeId.HasValue || assignment.ShiftTypeId == shiftTypeId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetApprovedLeavesByPeriodAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LeaveRequests
            .AsNoTracking()
            .Where(r => r.SchedulePeriodId == schedulePeriodId && r.Status == LeaveRequestStatus.Approved)
            .ToListAsync(cancellationToken);
    }

    public void Add(LeaveRequest leaveRequest)
    {
        _dbContext.LeaveRequests.Add(leaveRequest);
    }

    public void SetOriginalRowVersion(LeaveRequest request, byte[] rowVersion)
    {
        _dbContext.Entry(request).Property(x => x.RowVersion).OriginalValue = rowVersion;
    }

    private IQueryable<LeaveRequest> BaseQuery(bool asNoTracking)
    {
        IQueryable<LeaveRequest> query = _dbContext.LeaveRequests
            .Include(request => request.Employee)
                .ThenInclude(employee => employee.UserAccount)
            .Include(request => request.SchedulePeriod)
            .Include(request => request.ShiftType)
            .Include(request => request.ApprovedByAccount);

        return asNoTracking ? query.AsNoTracking() : query;
    }
}
