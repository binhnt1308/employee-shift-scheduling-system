namespace ShiftScheduling.Models.ViewModels;

// Đóng gói một trang dữ liệu cùng thông tin phân trang dùng chung giữa Service và ViewModel.
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int TotalPages => TotalItems == 0
        ? 1
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
