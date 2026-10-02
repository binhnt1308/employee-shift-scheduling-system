using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public interface IOrToolsSchedulingService
{
    /// <summary>
    /// Sinh phương án phân công tự động cho kỳ lịch từ nhu cầu nhân sự,
    /// lịch rảnh và các quy tắc xếp lịch hiện hành.
    /// </summary>
    OrToolsProposalResultViewModel GenerateProposal(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        IReadOnlyList<ShiftType> shiftTypes,
        int timeoutSeconds = 10);
}
