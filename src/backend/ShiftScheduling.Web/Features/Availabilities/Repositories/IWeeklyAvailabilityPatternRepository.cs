using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Repositories;

/// <summary>
/// Repository quản lý mẫu lịch rảnh cố định theo tuần (NC-02/QT-2).
/// </summary>
public interface IWeeklyAvailabilityPatternRepository
{
    /// <summary>
    /// Lấy toàn bộ mẫu tuần của nhân viên.
    /// </summary>
    Task<List<WeeklyAvailabilityPattern>> GetByEmployeeIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu mẫu tuần (upsert): cập nhật nếu đã có (EmployeeId+DayOfWeek+ShiftCode),
    /// thêm mới nếu chưa có, xóa các entry không còn trong danh sách mới.
    /// </summary>
    /// <param name="employeeId">ID nhân viên sở hữu mẫu.</param>
    /// <param name="entries">Danh sách entry mới; truyền rỗng để xóa toàn bộ mẫu.</param>
    /// <param name="now">Thời điểm hiện tại (UTC).</param>
    /// <param name="cancellationToken">Token hủy thao tác khi request kết thúc sớm.</param>
    Task UpsertByEmployeeAsync(
        int employeeId,
        IEnumerable<(int DayOfWeek, string ShiftCode, AvailabilityPreferenceLevel PreferenceLevel)> entries,
        DateTime now,
        CancellationToken cancellationToken = default);
}
