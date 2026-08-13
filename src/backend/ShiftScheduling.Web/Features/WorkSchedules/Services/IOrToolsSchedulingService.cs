using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public interface IOrToolsSchedulingService
{
    OrToolsProposalResultViewModel GenerateProposal(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        IReadOnlyList<ShiftType> shiftTypes,
        int timeoutSeconds = 10);
}
