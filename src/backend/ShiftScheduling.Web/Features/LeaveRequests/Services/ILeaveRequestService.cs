using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.LeaveRequests.Services;

public interface ILeaveRequestService
{
    /// <summary>
    /// Lấy danh sách đơn xin nghỉ theo phạm vi của Nhân viên hoặc Quản lý cửa hàng.
    /// </summary>
    Task<LeaveRequestIndexViewModel> GetIndexAsync(
        int actorAccountId,
        bool isManager,
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuẩn bị dữ liệu kỳ lịch và ca cố định cho biểu mẫu gửi đơn xin nghỉ.
    /// </summary>
    Task<LeaveRequestCreateViewModel?> PrepareCreateAsync(
        int actorAccountId,
        LeaveRequestCreateViewModel? model = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi đơn xin nghỉ của chính Nhân viên đang đăng nhập
    /// sau khi kiểm tra dữ liệu và trùng lặp.
    /// </summary>
    Task<ServiceResult> SubmitAsync(
        LeaveRequestCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết đơn xin nghỉ, bảo đảm Nhân viên chỉ xem đơn của bản thân.
    /// </summary>
    Task<LeaveRequestDetailsViewModel?> GetDetailsAsync(
        int leaveRequestId,
        int actorAccountId,
        bool isManager,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuẩn bị dữ liệu mới nhất để Quản lý xem xét đơn đang chờ duyệt.
    /// </summary>
    Task<LeaveRequestReviewViewModel?> PrepareReviewAsync(
        int leaveRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Duyệt hoặc từ chối đơn xin nghỉ và tạo thông báo kết quả cho Nhân viên.
    /// </summary>
    Task<ServiceResult> ReviewAsync(
        int leaveRequestId,
        bool approve,
        string? managerNote,
        int managerAccountId,
        string rowVersion = "",
        CancellationToken cancellationToken = default);
}
