using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Data;
using ShiftScheduling.Services.Common;
using ShiftScheduling.ViewModels.ShiftType;

namespace ShiftScheduling.Services.ShiftTypes;

public sealed class ShiftTypeService : IShiftTypeService
{
    private readonly ApplicationDbContext _dbContext;

    public ShiftTypeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ShiftTypeListItemViewModel>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShiftTypes
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new ShiftTypeListItemViewModel
            {
                ShiftTypeId = x.ShiftTypeId,
                ShiftCode = x.ShiftCode,
                ShiftName = x.ShiftName,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                DurationHours = x.DurationHours,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ShiftTypeEditViewModel?> GetEditAsync(
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ShiftTypes
            .AsNoTracking()
            .Where(x => x.ShiftTypeId == shiftTypeId)
            .Select(x => new ShiftTypeEditViewModel
            {
                ShiftTypeId = x.ShiftTypeId,
                ShiftCode = x.ShiftCode,
                ShiftName = x.ShiftName,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult> UpdateAsync(
        int shiftTypeId,
        ShiftTypeEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.EndTime <= model.StartTime)
        {
            return ServiceResult.Failure(
                "Giờ kết thúc phải lớn hơn giờ bắt đầu.");
        }

        var shiftType = await _dbContext.ShiftTypes
            .SingleOrDefaultAsync(
                x => x.ShiftTypeId == shiftTypeId,
                cancellationToken);

        if (shiftType is null)
        {
            return ServiceResult.Failure("Không tìm thấy ca làm việc.");
        }

        var displayOrderExists = await _dbContext.ShiftTypes
            .AnyAsync(
                x => x.ShiftTypeId != shiftTypeId
                    && x.DisplayOrder == model.DisplayOrder,
                cancellationToken);

        if (displayOrderExists)
        {
            return ServiceResult.Failure(
                "Thứ tự hiển thị đã được ca khác sử dụng.");
        }

        if (model.IsActive)
        {
            var overlaps = await _dbContext.ShiftTypes
                .AnyAsync(
                    x => x.ShiftTypeId != shiftTypeId
                        && x.IsActive
                        && model.StartTime < x.EndTime
                        && x.StartTime < model.EndTime,
                    cancellationToken);

            if (overlaps)
            {
                return ServiceResult.Failure(
                    "Khung giờ mới bị trùng với một ca đang hoạt động.");
            }
        }

        var duration = model.EndTime.ToTimeSpan() - model.StartTime.ToTimeSpan();

        shiftType.ShiftName = model.ShiftName.Trim();
        shiftType.StartTime = model.StartTime;
        shiftType.EndTime = model.EndTime;
        shiftType.DurationHours = (decimal)duration.TotalHours;
        shiftType.DisplayOrder = model.DisplayOrder;
        shiftType.IsActive = model.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }
}
