using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.ShiftTypes.Repositories;

// Khai báo các thao tác dữ liệu cần cho nghiệp vụ cấu hình ca làm việc; Service giữ quyền quyết định business rule.
public interface IShiftTypeRepository
{
    Task<IReadOnlyList<ShiftType>> GetAllAsync(
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftType>> GetActiveShiftsAsync(
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftType>> GetByIdsAsync(
        IEnumerable<int> ids,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<ShiftType?> GetByIdAsync(
        int shiftTypeId,
        bool asNoTracking,
        CancellationToken cancellationToken = default);

    Task<bool> DisplayOrderExistsAsync(
        int displayOrder,
        int excludedShiftTypeId,
        CancellationToken cancellationToken = default);

    Task<bool> OverlapsAsync(
        TimeOnly startTime,
        TimeOnly endTime,
        int excludedShiftTypeId,
        CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<ShiftType> shiftTypes);
}
