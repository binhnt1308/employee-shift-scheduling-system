using System.ComponentModel.DataAnnotations;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.ShiftTransfers.ViewModels;

public enum ShiftTransferStatusFilter
{
    WaitingReceiver = 1,
    WaitingManager = 2,
    Approved = 3,
    Rejected = 4
}

public sealed class ShiftTransferIndexViewModel
{
    public bool IsManager { get; init; }
    public ShiftTransferStatusFilter? ManagerStatus { get; init; }
    public IReadOnlyList<ShiftTransferListItemViewModel> SentRequests { get; init; } = [];
    public IReadOnlyList<ShiftTransferListItemViewModel> ReceivedRequests { get; init; } = [];
    public IReadOnlyList<ShiftTransferListItemViewModel> ManagerRequests { get; init; } = [];
}

public sealed class ShiftTransferListItemViewModel
{
    public int ShiftTransferRequestId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string PeriodName { get; init; } = string.Empty;
    public DateOnly WorkDate { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public ReceiverStatus ReceiverStatus { get; init; }
    public ApprovalStatus ApprovalStatus { get; init; }
    public DateTime SubmittedAt { get; init; }
    public bool CanRespond { get; init; }
    public bool CanReview { get; init; }
}

public sealed class ShiftTransferCreateViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn ca cần chuyển.")]
    [Display(Name = "Ca cần chuyển")]
    public int ShiftAssignmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn đồng nghiệp nhận ca.")]
    [Display(Name = "Người nhận ca")]
    public int ReceiverEmployeeId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung cần trao đổi.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Nội dung phải từ 3 đến 1000 ký tự.")]
    [Display(Name = "Nội dung trao đổi")]
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<ShiftAssignmentOptionViewModel> Assignments { get; set; } = [];
    public IReadOnlyList<ShiftReceiverOptionViewModel> Receivers { get; set; } = [];
}

public sealed class ShiftTransferDetailsViewModel
{
    public int ShiftTransferRequestId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string PeriodName { get; init; } = string.Empty;
    public DateOnly WorkDate { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string PositionName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public ReceiverStatus ReceiverStatus { get; init; }
    public ApprovalStatus ApprovalStatus { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReceiverRespondedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ManagerNote { get; init; }
    public bool CanRespond { get; init; }
    public bool CanReview { get; init; }
}

public sealed class ShiftTransferReviewViewModel
{
    public int ShiftTransferRequestId { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    [Display(Name = "Ghi chú của Quản lý")]
    public string? ManagerNote { get; set; }

    public ShiftTransferDetailsViewModel Details { get; set; } = new();
    public IReadOnlyList<string> BlockingErrors { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public sealed record ShiftAssignmentOptionViewModel(
    int ShiftAssignmentId,
    string PeriodName,
    DateOnly WorkDate,
    string ShiftName,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed record ShiftReceiverOptionViewModel(
    int EmployeeId,
    string EmployeeCode,
    string FullName,
    EmployeeLevel Level,
    EmploymentType EmploymentType);
