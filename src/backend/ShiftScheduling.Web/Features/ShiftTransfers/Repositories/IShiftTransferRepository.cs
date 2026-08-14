using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTransfers.Repositories;

public interface IShiftTransferRepository
{
    Task<Employee?> GetEmployeeByAccountIdAsync(
        int accountId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftAssignment>> GetPublishedAssignmentsForEmployeeAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<ShiftAssignment?> GetAssignmentByIdAsync(
        int shiftAssignmentId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Employee>> GetActiveEmployeesByPositionAsync(
        EmployeePosition position,
        int excludedEmployeeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasAvailabilityAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasApprovedLeaveAsync(
        int employeeId,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftAssignment>> GetPublishedAssignmentsForEmployeeInPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftAssignment>> GetAssignmentsForShiftAsync(
        int workScheduleId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<StaffingRequirement?> GetStaffingRequirementAsync(
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveRequestForAssignmentAsync(
        int shiftAssignmentId,
        CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetCandidateAvailabilitiesAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<HashSet<int>> GetCandidateApprovedLeavesAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<ILookup<int, ShiftAssignment>> GetCandidatePublishedAssignmentsInPeriodAsync(
        IEnumerable<int> candidateEmployeeIds,
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftTransferRequest>> GetSentRequestsAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ShiftTransferRequest>> GetPagedSentRequestsAsync(
        int employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftTransferRequest>> GetReceivedRequestsAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ShiftTransferRequest>> GetPagedReceivedRequestsAsync(
        int employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftTransferRequest>> GetManagerRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResult<ShiftTransferRequest>> GetPagedManagerRequestsAsync(
        ShiftTransferStatusFilter? filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ShiftTransferRequest?> GetRequestByIdAsync(
        int shiftTransferRequestId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    void Add(ShiftTransferRequest request);

    void SetOriginalRowVersion(ShiftTransferRequest request, byte[] rowVersion);
}
