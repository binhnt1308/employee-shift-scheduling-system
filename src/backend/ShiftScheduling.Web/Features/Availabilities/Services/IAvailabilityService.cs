using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Availabilities.Services;

public interface IAvailabilityService
{
    /// <summary>Lấy danh sách kỳ lịch và trạng thái đăng ký lịch rảnh của nhân viên.</summary>
    Task<List<AvailabilityPeriodItemViewModel>> GetEmployeePeriodsAsync(
        int accountId,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy form đăng ký/cập nhật lịch rảnh kỳ hiện tại, kèm mẫu tuần đã lưu.</summary>
    Task<AvailabilityRegisterViewModel?> GetEmployeeRegisterFormAsync(
        int accountId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu lịch rảnh của nhân viên cho kỳ lịch.
    /// Validate: kỳ Open + chưa hết hạn + Employee active + ShiftTypeId hợp lệ.
    /// ShiftPreferences chỉ được nhận cho các key có trong selectedShiftKeys;
    /// preference của ca không được chọn bị bỏ qua hoàn toàn.
    /// </summary>
    Task<ServiceResult> SaveEmployeeAvailabilityAsync(
        int accountId,
        int schedulePeriodId,
        List<string> selectedShiftKeys,
        Dictionary<string, AvailabilityPreferenceLevel>? shiftPreferences = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy tổng quan lịch rảnh nhân viên cho Manager.</summary>
    Task<ManagerAvailabilityOverviewViewModel?> GetManagerOverviewAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu mẫu lịch rảnh tuần cho nhân viên (NC-02).
    /// Kiểm tra nhân viên đang hoạt động, DayOfWeek từ 1 đến 7, ShiftCode cố định và PreferenceLevel hợp lệ.
    /// </summary>
    Task<ServiceResult> SaveWeeklyPatternAsync(
        int accountId,
        List<WeeklyPatternEntryInputViewModel> entries,
        CancellationToken cancellationToken = default);
}
