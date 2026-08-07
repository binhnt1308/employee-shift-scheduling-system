using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.ShiftTypes.Services;

/// <summary>
/// Service quan ly cac ca lam viec co dinh theo BR03.
/// </summary>
public sealed class ShiftTypeService : IShiftTypeService
{
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ShiftTypeService> _logger;

    public ShiftTypeService(
        IShiftTypeRepository shiftTypeRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ShiftTypeService> logger)
    {
        _shiftTypeRepository = shiftTypeRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShiftTypeListItemViewModel>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var shiftTypes = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        return shiftTypes.Select(MapListItem).ToArray();
    }

    /// <inheritdoc />
    public async Task<ShiftTypeEditViewModel?> GetEditAsync(
        int shiftTypeId,
        CancellationToken cancellationToken = default)
    {
        var shiftType = await _shiftTypeRepository.GetByIdAsync(shiftTypeId, true, cancellationToken);
        return shiftType is null ? null : MapEdit(shiftType);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(
        int shiftTypeId,
        ShiftTypeEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var shiftType = await _shiftTypeRepository.GetByIdAsync(shiftTypeId, false, cancellationToken);
        if (shiftType is null)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTypeNotFound, "Không tìm thấy ca làm việc.");
        }

        // BR03: He thong chi su dung dung ba ca co dinh (Morning 08-12, Afternoon 12-16, Evening 16-20).
        if (!ShiftTypeConstants.IsValidFixedShiftTime(shiftType.ShiftCode, model.StartTime, model.EndTime))
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftTimeInvalid,
                "BR03 quy định 3 ca cố định: Ca sáng (08:00-12:00), Ca chiều (12:00-16:00), Ca tối (16:00-20:00). Không được thay đổi khung giờ.");
        }

        if (model.EndTime <= model.StartTime)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeInvalid, "Giờ kết thúc phải lớn hơn giờ bắt đầu.");
        }

        if (!model.IsActive)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeInvalid, "Không được ngưng hoạt động ca cố định của hệ thống.");
        }

        var validation = await ValidateUniqueValuesAsync(shiftTypeId, model, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var previousValues = BuildAuditValues(shiftType);
        ApplyChanges(shiftType, model);
        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "UPDATE_SHIFT_TYPE",
            nameof(ShiftType),
            shiftType.ShiftTypeId.ToString(),
            previousValues,
            BuildAuditValues(shiftType),
            _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shift type updated. ActorAccountId: {ActorAccountId}; ShiftTypeId: {ShiftTypeId}",
            actorAccountId,
            shiftTypeId);
        return ServiceResult.Success();
    }

    private async Task<ServiceResult> ValidateUniqueValuesAsync(
        int shiftTypeId,
        ShiftTypeEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (await _shiftTypeRepository.DisplayOrderExistsAsync(
                model.DisplayOrder,
                shiftTypeId,
                cancellationToken))
        {
            return ServiceResult.Failure(
                ErrorCodes.DuplicateDisplayOrder,
                "Thứ tự hiển thị đã được ca khác sử dụng.");
        }

        // BR03: Ba ca co dinh khong duoc trung khung gio.
        if (model.IsActive
            && await _shiftTypeRepository.OverlapsAsync(
                model.StartTime,
                model.EndTime,
                shiftTypeId,
                cancellationToken))
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeOverlap, "Khung giờ bị trùng với ca đang hoạt động.");
        }

        return ServiceResult.Success();
    }

    private static void ApplyChanges(ShiftType shiftType, ShiftTypeEditViewModel model)
    {
        var duration = model.EndTime.ToTimeSpan() - model.StartTime.ToTimeSpan();
        shiftType.ShiftName = model.ShiftName.Trim();
        shiftType.StartTime = model.StartTime;
        shiftType.EndTime = model.EndTime;
        shiftType.DurationHours = decimal.Round((decimal)duration.TotalHours, 2);
        shiftType.DisplayOrder = model.DisplayOrder;
        shiftType.IsActive = model.IsActive;
    }

    private static object BuildAuditValues(ShiftType shiftType)
    {
        return new
        {
            shiftType.ShiftCode,
            shiftType.ShiftName,
            shiftType.StartTime,
            shiftType.EndTime,
            shiftType.DurationHours,
            shiftType.DisplayOrder,
            shiftType.IsActive
        };
    }

    private static ShiftTypeListItemViewModel MapListItem(ShiftType shiftType)
    {
        return new ShiftTypeListItemViewModel
        {
            ShiftTypeId = shiftType.ShiftTypeId,
            ShiftCode = shiftType.ShiftCode,
            ShiftName = shiftType.ShiftName,
            StartTime = shiftType.StartTime,
            EndTime = shiftType.EndTime,
            DurationHours = shiftType.DurationHours,
            DisplayOrder = shiftType.DisplayOrder,
            IsActive = shiftType.IsActive
        };
    }

    private static ShiftTypeEditViewModel MapEdit(ShiftType shiftType)
    {
        return new ShiftTypeEditViewModel
        {
            ShiftTypeId = shiftType.ShiftTypeId,
            ShiftCode = shiftType.ShiftCode,
            ShiftName = shiftType.ShiftName,
            StartTime = shiftType.StartTime,
            EndTime = shiftType.EndTime,
            DisplayOrder = shiftType.DisplayOrder,
            IsActive = shiftType.IsActive
        };
    }
}
