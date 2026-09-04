using Microsoft.EntityFrameworkCore;
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
/// Service quản lý cấu hình các ca làm việc theo phiên bản (versioned shift configuration).
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
        var shiftTypes = await _shiftTypeRepository.GetActiveShiftsAsync(true, cancellationToken);
        return shiftTypes.Select(MapListItem).ToArray();
    }

    /// <inheritdoc />
    public async Task<ShiftConfigurationEditViewModel?> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        var activeShifts = await _shiftTypeRepository.GetActiveShiftsAsync(true, cancellationToken);
        if (!IsValidActiveShiftSet(activeShifts, out var morning, out var afternoon, out var evening))
        {
            return null;
        }

        var version = morning!.ConfigurationVersion;
        var ordered = new[] { morning, afternoon!, evening! }.OrderBy(s => s.DisplayOrder).ToList();
        return new ShiftConfigurationEditViewModel
        {
            ExpectedConfigurationVersion = version,
            MaxShiftsPerEmployeePerDay = morning!.MaxShiftsPerEmployeePerDay,
            AllowSplitShift = morning.AllowSplitShift,
            Shifts = ordered.Select(s => new ShiftConfigurationItemViewModel
            {
                ShiftTypeId = s.ShiftTypeId,
                ShiftCode = s.ShiftCode,
                ShiftName = s.ShiftName,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                DurationHours = s.DurationHours,
                DisplayOrder = s.DisplayOrder
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateConfigurationAsync(
        ShiftConfigurationEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var modelValidation = ValidateModel(model, out var morning, out var afternoon, out var evening);
        if (!modelValidation.Succeeded)
        {
            return modelValidation;
        }

        var activeShifts = (await _shiftTypeRepository.GetActiveShiftsAsync(false, cancellationToken))
            .OrderBy(s => s.DisplayOrder)
            .ToList();

        var activeValidation = ValidateActiveShifts(activeShifts, model.ExpectedConfigurationVersion, out var activeMorning, out var activeAfternoon, out var activeEvening);
        if (!activeValidation.Succeeded)
        {
            return activeValidation;
        }

        var timeValidation = ValidateShiftTimesAndDurations(morning!, afternoon!, evening!);
        if (!timeValidation.Succeeded)
        {
            return timeValidation;
        }

        var effectiveAllowSplitShift = model.MaxShiftsPerEmployeePerDay == 2 && model.AllowSplitShift;

        var isUnchanged =
            activeMorning!.StartTime == morning!.StartTime && activeMorning.EndTime == morning.EndTime &&
            activeAfternoon!.StartTime == afternoon!.StartTime && activeAfternoon.EndTime == afternoon.EndTime &&
            activeEvening!.StartTime == evening!.StartTime && activeEvening.EndTime == evening.EndTime &&
            activeMorning.MaxShiftsPerEmployeePerDay == model.MaxShiftsPerEmployeePerDay &&
            activeMorning.AllowSplitShift == effectiveAllowSplitShift;

        if (isUnchanged)
        {
            return ServiceResult.Success("Cấu hình ca làm việc không có thay đổi.");
        }

        var currentVersion = activeMorning!.ConfigurationVersion;
        var newVersion = currentVersion + 1;

        DeactivateOldShifts(activeShifts);
        var newShifts = CreateNewVersionShifts(activeShifts, morning!, afternoon!, evening!, newVersion, model.MaxShiftsPerEmployeePerDay, effectiveAllowSplitShift);
        _shiftTypeRepository.AddRange(newShifts);

        LogAuditRecord(actorAccountId, newVersion, activeShifts, newShifts);
        return await SaveConfigurationChangesAsync(actorAccountId, newVersion, cancellationToken);
    }

    private static bool IsValidActiveShiftSet(
        IReadOnlyList<ShiftType> activeShifts,
        out ShiftType? morning,
        out ShiftType? afternoon,
        out ShiftType? evening)
    {
        morning = null;
        afternoon = null;
        evening = null;

        if (activeShifts.Count != 3)
        {
            return false;
        }

        morning = activeShifts.FirstOrDefault(s => string.Equals(s.ShiftCode, ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase));
        afternoon = activeShifts.FirstOrDefault(s => string.Equals(s.ShiftCode, ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase));
        evening = activeShifts.FirstOrDefault(s => string.Equals(s.ShiftCode, ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase));

        if (morning is null || afternoon is null || evening is null)
        {
            return false;
        }

        var version = morning.ConfigurationVersion;
        if (afternoon.ConfigurationVersion != version || evening.ConfigurationVersion != version)
        {
            return false;
        }

        if (morning.MaxShiftsPerEmployeePerDay != afternoon.MaxShiftsPerEmployeePerDay
            || morning.MaxShiftsPerEmployeePerDay != evening.MaxShiftsPerEmployeePerDay
            || morning.AllowSplitShift != afternoon.AllowSplitShift
            || morning.AllowSplitShift != evening.AllowSplitShift)
        {
            return false;
        }

        if (morning.StartTime >= morning.EndTime || afternoon.StartTime >= afternoon.EndTime || evening.StartTime >= evening.EndTime)
        {
            return false;
        }

        return true;
    }

    private static ServiceResult ValidateModel(
        ShiftConfigurationEditViewModel model,
        out ShiftConfigurationItemViewModel? morning,
        out ShiftConfigurationItemViewModel? afternoon,
        out ShiftConfigurationItemViewModel? evening)
    {
        morning = null;
        afternoon = null;
        evening = null;

        if (model.Shifts == null || model.Shifts.Count != 3)
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftConfigurationIncomplete,
                "Cấu hình phải có đủ ba ca làm việc.");
        }

        if (model.MaxShiftsPerEmployeePerDay is not (1 or 2))
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftConfigurationInvalid,
                "Số ca tối đa mỗi nhân viên trong ngày chỉ có thể là 1 hoặc 2.");
        }

        morning = model.Shifts.FirstOrDefault(s => s.ShiftCode == ShiftTypeConstants.MorningCode);
        afternoon = model.Shifts.FirstOrDefault(s => s.ShiftCode == ShiftTypeConstants.AfternoonCode);
        evening = model.Shifts.FirstOrDefault(s => s.ShiftCode == ShiftTypeConstants.EveningCode);

        if (morning is null || afternoon is null || evening is null)
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftConfigurationIncomplete,
                "Cấu hình phải có đủ ba ca: Ca sáng (MORNING), Ca chiều (AFTERNOON) và Ca tối (EVENING).");
        }

        return ServiceResult.Success();
    }

    private static ServiceResult ValidateActiveShifts(
        IReadOnlyList<ShiftType> activeShifts,
        int expectedConfigurationVersion,
        out ShiftType? morning,
        out ShiftType? afternoon,
        out ShiftType? evening)
    {
        if (!IsValidActiveShiftSet(activeShifts, out morning, out afternoon, out evening))
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftConfigurationInvalid,
                "Hệ thống không có đủ ba ca làm việc đang hoạt động hợp lệ hoặc phiên bản không đồng nhất.");
        }

        var currentVersion = morning!.ConfigurationVersion;
        if (expectedConfigurationVersion <= 0 || expectedConfigurationVersion != currentVersion)
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftConfigurationChanged,
                "Cấu hình ca làm việc đã được người khác cập nhật trước đó. Vui lòng tải lại trang.");
        }

        return ServiceResult.Success();
    }

    private static ServiceResult ValidateShiftTimesAndDurations(
        ShiftConfigurationItemViewModel morning,
        ShiftConfigurationItemViewModel afternoon,
        ShiftConfigurationItemViewModel evening)
    {
        if (morning.EndTime <= morning.StartTime)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeInvalid, "Giờ kết thúc Ca sáng phải lớn hơn giờ bắt đầu.");
        }

        if (afternoon.EndTime <= afternoon.StartTime)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeInvalid, "Giờ kết thúc Ca chiều phải lớn hơn giờ bắt đầu.");
        }

        if (evening.EndTime <= evening.StartTime)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeInvalid, "Giờ kết thúc Ca tối phải lớn hơn giờ bắt đầu.");
        }

        // BR-01: Ba ca làm việc trong ngày không được chồng lấn thời gian.
        if (morning.EndTime > afternoon.StartTime)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeOverlap, "Giờ kết thúc Ca sáng không được lớn hơn giờ bắt đầu Ca chiều.");
        }

        // BR-01: Ba ca làm việc trong ngày không được chồng lấn thời gian.
        if (afternoon.EndTime > evening.StartTime)
        {
            return ServiceResult.Failure(ErrorCodes.ShiftTimeOverlap, "Giờ kết thúc Ca chiều không được lớn hơn giờ bắt đầu Ca tối.");
        }

        return ServiceResult.Success();
    }

    private static void DeactivateOldShifts(IEnumerable<ShiftType> activeShifts)
    {
        foreach (var oldShift in activeShifts)
        {
            oldShift.IsActive = false;
        }
    }

    private static List<ShiftType> CreateNewVersionShifts(
        IReadOnlyList<ShiftType> activeShifts,
        ShiftConfigurationItemViewModel morning,
        ShiftConfigurationItemViewModel afternoon,
        ShiftConfigurationItemViewModel evening,
        int newVersion,
        int maxShiftsPerDay,
        bool allowSplitShift)
    {
        var inputMap = new Dictionary<string, ShiftConfigurationItemViewModel>
        {
            [ShiftTypeConstants.MorningCode] = morning,
            [ShiftTypeConstants.AfternoonCode] = afternoon,
            [ShiftTypeConstants.EveningCode] = evening
        };

        var newShifts = new List<ShiftType>();
        foreach (var oldShift in activeShifts)
        {
            var input = inputMap[oldShift.ShiftCode];
            var durationMinutes = (input.EndTime.ToTimeSpan() - input.StartTime.ToTimeSpan()).TotalMinutes;
            var durationHours = decimal.Round((decimal)(durationMinutes / 60.0), 2);

            newShifts.Add(new ShiftType
            {
                ShiftCode = oldShift.ShiftCode,
                ShiftName = oldShift.ShiftName,
                DisplayOrder = oldShift.DisplayOrder,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                DurationHours = durationHours,
                ConfigurationVersion = newVersion,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = maxShiftsPerDay,
                AllowSplitShift = allowSplitShift
            });
        }

        return newShifts;
    }

    private void LogAuditRecord(
        int actorAccountId,
        int newVersion,
        IReadOnlyList<ShiftType> activeShifts,
        IReadOnlyList<ShiftType> newShifts)
    {
        var oldAudit = activeShifts.Select(s => new { s.ShiftCode, s.StartTime, s.EndTime, s.DurationHours, s.ConfigurationVersion, s.MaxShiftsPerEmployeePerDay, s.AllowSplitShift }).ToList();
        var newAudit = newShifts.Select(s => new { s.ShiftCode, s.StartTime, s.EndTime, s.DurationHours, s.ConfigurationVersion, s.MaxShiftsPerEmployeePerDay, s.AllowSplitShift }).ToList();

        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId, "UPDATE_SHIFT_CONFIGURATION", nameof(ShiftType), newVersion.ToString(), oldAudit, newAudit, _clock.UtcNow));
    }

    private async Task<ServiceResult> SaveConfigurationChangesAsync(
        int actorAccountId,
        int newVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsShiftConfigurationVersionConflict(ex))
        {
            _logger.LogWarning(ex, "Concurrency conflict on shift configuration version.");
            return ServiceResult.Failure(ErrorCodes.ShiftConfigurationChanged, "Cấu hình ca làm việc đã được người khác cập nhật trước đó.");
        }

        _logger.LogInformation("Shift configuration updated. ActorAccountId: {ActorAccountId}; NewVersion: {NewVersion}", actorAccountId, newVersion);
        return ServiceResult.Success();
    }

    private static bool IsShiftConfigurationVersionConflict(DbUpdateException ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            if (current is Microsoft.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                var msg = sqlEx.Message ?? string.Empty;
                if (msg.Contains("IX_ShiftTypes_ShiftCode_ConfigurationVersion", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("IX_ShiftTypes_DisplayOrder_ConfigurationVersion", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            current = current.InnerException;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<ShiftTypeEditViewModel?> GetEditAsync(int shiftTypeId, CancellationToken cancellationToken = default)
    {
        var shiftType = await _shiftTypeRepository.GetByIdAsync(shiftTypeId, true, cancellationToken);
        return shiftType is null ? null : MapEdit(shiftType);
    }

    /// <inheritdoc />
    public Task<ServiceResult> UpdateAsync(int shiftTypeId, ShiftTypeEditViewModel model, int actorAccountId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult.Failure(ErrorCodes.ShiftTimeInvalid, "Không được thay đổi riêng lẻ ca cố định. Vui lòng sử dụng chức năng Cấu hình giờ 3 ca."));
    }

    private static ShiftTypeListItemViewModel MapListItem(ShiftType shiftType) => new()
    {
        ShiftTypeId = shiftType.ShiftTypeId,
        ShiftCode = shiftType.ShiftCode,
        ShiftName = shiftType.ShiftName,
        StartTime = shiftType.StartTime,
        EndTime = shiftType.EndTime,
        DurationHours = shiftType.DurationHours,
        DisplayOrder = shiftType.DisplayOrder,
        ConfigurationVersion = shiftType.ConfigurationVersion,
        IsActive = shiftType.IsActive
    };

    private static ShiftTypeEditViewModel MapEdit(ShiftType shiftType) => new()
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
