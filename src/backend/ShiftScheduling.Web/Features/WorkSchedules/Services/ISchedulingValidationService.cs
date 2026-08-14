using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Service kiểm tra các quy tắc và ràng buộc khi lập lịch làm việc.
/// </summary>
public interface ISchedulingValidationService
{
    /// <summary>
    /// Kiểm tra toàn bộ các ràng buộc bắt buộc và cảnh báo ưu tiên cho lịch làm việc.
    /// </summary>
    /// <param name="period">Kỳ lịch cần kiểm tra.</param>
    /// <param name="activeEmployees">Danh sách nhân viên đang hoạt động.</param>
    /// <param name="availabilities">Danh sách đăng ký rảnh của nhân viên trong kỳ lịch.</param>
    /// <param name="approvedLeaves">Danh sách đơn xin nghỉ đã được duyệt trong kỳ lịch.</param>
    /// <param name="staffingRequirements">Danh sách định mức nhân sự cho các ca.</param>
    /// <param name="assignments">Danh sách ca làm việc đã được phân công.</param>
    /// <param name="shiftTypes">Danh sách các loại ca làm việc trong hệ thống.</param>
    /// <returns>
    /// Kết quả kiểm tra gồm danh sách lỗi bắt buộc, cảnh báo
    /// và tóm tắt công việc của từng nhân viên.
    /// </returns>
    ScheduleValidationResultViewModel ValidateSchedule(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        IReadOnlyList<ShiftAssignment> assignments,
        IReadOnlyList<ShiftType> shiftTypes);
}
