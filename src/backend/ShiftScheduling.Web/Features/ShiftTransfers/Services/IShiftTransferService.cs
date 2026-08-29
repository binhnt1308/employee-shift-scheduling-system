using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.ShiftTransfers.Services;

public interface IShiftTransferService
{
    /// <summary>
    /// Lấy danh sách yêu cầu đổi ca theo phạm vi của Nhân viên hoặc Quản lý cửa hàng.
    /// </summary>
    Task<ShiftTransferIndexViewModel> GetIndexAsync(
        int actorAccountId,
        bool isManager,
        ShiftTransferStatusFilter? managerStatus,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuẩn bị các ca chính thức của người gửi và đồng nghiệp có khả năng nhận ca.
    /// </summary>
    Task<ShiftTransferCreateViewModel?> PrepareCreateAsync(
        int actorAccountId,
        ShiftTransferCreateViewModel? model = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi yêu cầu chuyển một ca chính thức cho một đồng nghiệp nhận thay.
    /// </summary>
    Task<ServiceResult> SubmitAsync(
        ShiftTransferCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết yêu cầu theo quyền của người gửi, người nhận hoặc Quản lý.
    /// </summary>
    Task<ShiftTransferDetailsViewModel?> GetDetailsAsync(
        int shiftTransferRequestId,
        int actorAccountId,
        bool isManager,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận phản hồi đồng ý hoặc từ chối nhận ca của đúng người nhận.
    /// </summary>
    Task<ServiceResult> RespondAsync(
        int shiftTransferRequestId,
        bool accept,
        int actorAccountId,
        string rowVersion = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chuẩn bị kết quả kiểm tra ràng buộc mới nhất trước khi Quản lý xử lý yêu cầu.
    /// </summary>
    Task<ShiftTransferReviewViewModel?> PrepareReviewAsync(
        int shiftTransferRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Duyệt hoặc từ chối yêu cầu và cập nhật lịch chính thức khi đủ điều kiện.
    /// </summary>
    Task<ServiceResult> ReviewAsync(
        int shiftTransferRequestId,
        bool approve,
        string? managerNote,
        int managerAccountId,
        string rowVersion = "",
        CancellationToken cancellationToken = default);
}
