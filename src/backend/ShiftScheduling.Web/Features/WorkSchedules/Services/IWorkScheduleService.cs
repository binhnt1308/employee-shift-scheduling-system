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

    /// <summary>
    /// Lấy thông tin đơn nghỉ đã duyệt và danh sách ứng viên phù hợp để xếp thay thế cho các ca bị thiếu nhân sự.
    /// </summary>
    /// <param name="leaveRequestId">Mã đơn xin nghỉ đã được duyệt.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>
    /// ViewModel chi tiết đơn nghỉ và danh sách ca kèm ứng viên hợp lệ, hoặc null nếu không tìm thấy.
    /// </returns>
    Task<LeaveReplacementViewModel?> GetLeaveReplacementAsync(
        int leaveRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật trực tiếp nhân viên thay thế vào phân công ca bị ảnh hưởng bởi đơn nghỉ đã duyệt trên lịch đã công bố.
    /// </summary>
    /// <param name="model">Dữ liệu yêu cầu thay thế bao gồm mã đơn nghỉ, mã phân công ca và mã nhân viên thay.</param>
    /// <param name="managerAccountId">Mã tài khoản Quản lý thực hiện xác nhận thay thế.</param>
    /// <param name="cancellationToken">Token hủy thao tác không đồng bộ.</param>
    /// <returns>Kết quả thực hiện thao tác thay thế ca làm việc.</returns>
    Task<ServiceResult> ReplaceApprovedLeaveAssignmentAsync(
        LeaveReplacementRequestViewModel model,
        int managerAccountId,
        CancellationToken cancellationToken = default);
}
