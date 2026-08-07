using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public interface IScheduleValidationService
{
    ScheduleValidationResultViewModel ValidateSchedule(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyList<ShiftType> shiftTypes);
}
