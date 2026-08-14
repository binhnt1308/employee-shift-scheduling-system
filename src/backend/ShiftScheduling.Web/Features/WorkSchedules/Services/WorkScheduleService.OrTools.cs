using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public partial class WorkScheduleService
{
    /// <inheritdoc />
    public async Task<OrToolsProposalResultViewModel> GenerateOrToolsProposalAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return new OrToolsProposalResultViewModel
            {
                Succeeded = false,
                Status = "NOT_FOUND",
                Message = "Không tìm thấy kỳ lịch."
            };
        }

        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, null, null, cancellationToken);
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);

        // BR15: OR-Tools chỉ sinh lịch đề xuất trong bộ nhớ, KHÔNG tự động lưu DB hay công bố
        var proposal = _orToolsService.GenerateProposal(
            period, activeEmployees, availabilities, leaves, period.StaffingRequirements.ToList(), allShifts, 10);

        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "GENERATE_ORTOOLS_PROPOSAL",
            nameof(SchedulePeriod),
            period.SchedulePeriodId.ToString(),
            null,
            new { schedulePeriodId, proposal.Succeeded, proposal.Status, proposal.ProposedAssignments.Count },
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return proposal;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> AdoptProposalToDraftAsync(
        int schedulePeriodId,
        List<ProposedAssignmentItemViewModel> proposedAssignments,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _workScheduleRepository.EnsureDraftExistsAsync(
            schedulePeriodId, accountId, _clock.UtcNow, cancellationToken);

        schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, false, true, cancellationToken);

        if (schedule is null || schedule.Status == ScheduleStatus.Published)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus,
                "Lịch đã công bố không thể ghi đè.");
        }

        var newAssignments = proposedAssignments.Select(p => (
            p.EmployeeId,
            p.WorkDate,
            p.ShiftTypeId,
            AssignmentSource.ORTools
        )).ToList();

        schedule.IsGeneratedByORTools = true;
        schedule.UpdatedAt = _clock.UtcNow;

        await _workScheduleRepository.SaveAssignmentsAsync(
            schedule.WorkScheduleId, newAssignments, _clock.UtcNow, cancellationToken);

        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "ADOPT_ORTOOLS_PROPOSAL",
            nameof(WorkSchedule),
            schedule.WorkScheduleId.ToString(),
            null,
            new { schedulePeriodId, Count = newAssignments.Count },
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Adopted OR-Tools proposal into Draft. ActorAccountId: {ActorAccountId};"
                + " PeriodId: {PeriodId}; Count: {Count}",
            accountId, schedulePeriodId, newAssignments.Count);

        return ServiceResult.Success();
    }
}
