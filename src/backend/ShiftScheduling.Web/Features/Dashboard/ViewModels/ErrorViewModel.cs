namespace ShiftScheduling.Models;

// Chứa các ViewModel dùng để trao đổi dữ liệu giữa Controller và View của chức năng Dashboard tổng quan.
public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
}
