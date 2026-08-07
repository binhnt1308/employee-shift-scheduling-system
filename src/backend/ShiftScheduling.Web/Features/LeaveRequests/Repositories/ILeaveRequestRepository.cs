using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.LeaveRequests.Repositories;

public interface ILeaveRequestRepository
{
    Task<Employee?> GetEmployeeByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchedulePeriod>> GetPeriodsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftType>> GetActiveShiftTypesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequest>> GetForEmployeeAsync(
        int employeeId,
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequest>> GetForManagerAsync(
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        CancellationToken cancellationToken = default);

    Task<LeaveRequest?> GetByIdAsync(
        int leaveRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<bool> HasEffectiveDuplicateAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPublishedAssignmentAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasDraftAssignmentAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly leaveDate,
        int? shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequest>> GetApprovedLeavesByPeriodAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    void Add(LeaveRequest leaveRequest);
}
