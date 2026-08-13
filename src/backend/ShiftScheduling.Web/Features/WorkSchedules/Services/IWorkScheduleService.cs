using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public interface IWorkScheduleService
{
    Task<WorkScheduleIndexViewModel> GetIndexAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<WorkScheduleEditViewModel?> GetEditGridAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<List<EligibleEmployeeOptionViewModel>> GetEligibleEmployeesForShiftAsync(
        int schedulePeriodId,
        DateOnly workDate,
        int shiftTypeId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> AssignEmployeeManualAsync(
        ManualAssignRequestViewModel model,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> RemoveEmployeeManualAsync(
        ManualRemoveRequestViewModel model,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SaveDraftAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<OrToolsProposalResultViewModel> GenerateOrToolsProposalAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> AdoptProposalToDraftAsync(
        int schedulePeriodId,
        List<ProposedAssignmentItemViewModel> proposedAssignments,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> PublishScheduleAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default);

    Task<WorkScheduleEditViewModel?> GetEmployeeMyScheduleAsync(
        int accountId,
        int? schedulePeriodId = null,
        CancellationToken cancellationToken = default);
}
