using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.ShiftTypes.Repositories;

public interface IShiftTypeRepository
{
    Task<IReadOnlyList<ShiftType>> GetAllAsync(
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
}
