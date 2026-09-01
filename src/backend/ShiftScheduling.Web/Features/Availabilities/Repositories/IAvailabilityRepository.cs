using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Repositories;

public interface IAvailabilityRepository
{
    Task<List<Availability>> GetByEmployeeAndPeriodAsync(
        int employeeId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default);

    Task<List<Availability>> GetByPeriodWithDetailsAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu lịch rảnh của nhân viên cho kỳ lịch, kèm mức nguyện vọng từng ca.
    /// Xóa các ca không còn được chọn, cập nhật các ca còn chọn, thêm ca mới.
    /// </summary>
    /// <param name="employeeId">ID nhân viên.</param>
    /// <param name="schedulePeriodId">ID kỳ lịch.</param>
    /// <param name="selectedShifts">Danh sách ca được chọn.</param>
    /// <param name="preferenceMap">Ánh xạ (WorkDate, ShiftTypeId) → PreferenceLevel cho các ca được chọn.</param>
    /// <param name="now">Thời điểm hiện tại (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveEmployeeAvailabilitiesAsync(
        int employeeId,
        int schedulePeriodId,
        IEnumerable<(DateOnly WorkDate, int ShiftTypeId)> selectedShifts,
        IReadOnlyDictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel> preferenceMap,
        DateTime now,
        CancellationToken cancellationToken = default);
}
