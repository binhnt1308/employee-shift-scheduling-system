using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

/// <summary>
/// Service quản lý kỳ lịch và định mức nhân sự theo FR03/UC03.
/// </summary>
public sealed partial class SchedulePeriodService : ISchedulePeriodService
{
    private readonly ISchedulePeriodRepository _schedulePeriodRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<SchedulePeriodService> _logger;

    public SchedulePeriodService(
        ISchedulePeriodRepository schedulePeriodRepository,
        IShiftTypeRepository shiftTypeRepository,
        INotificationRepository notificationRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<SchedulePeriodService> logger)
    {
        _schedulePeriodRepository = schedulePeriodRepository;
        _shiftTypeRepository = shiftTypeRepository;
        _notificationRepository = notificationRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SchedulePeriodIndexViewModel> GetIndexAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var paged = await _schedulePeriodRepository.GetPagedAsync(page, pageSize, cancellationToken);
        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));

        var items = paged.Items.Select(sp =>
        {
            var workSchedule = sp.WorkSchedules.OrderByDescending(w => w.WorkScheduleId).FirstOrDefault();
            var statusResult = SchedulePeriodStatusHelper.CalculateEffectiveStatus(sp, workSchedule, now, currentDate);

            return new SchedulePeriodListItemViewModel
            {
                SchedulePeriodId = sp.SchedulePeriodId,
                PeriodName = sp.PeriodName,
                WeekStartDate = sp.WeekStartDate,
                WeekEndDate = sp.WeekEndDate,
                AvailabilityDeadline = _clock.ToVietnamTime(sp.AvailabilityDeadline),
                RegistrationStatus = sp.RegistrationStatus,
                EffectiveStatusName = statusResult.StatusName,
                NeedsAttention = statusResult.NeedsAttention,
                DisplayBadgeClass = statusResult.DisplayBadgeClass,
                CreatedByUsername = sp.CreatedByAccount?.Username ?? string.Empty,
                TotalRequirementsCount = sp.StaffingRequirements?.Count(
                    r => IsFixedShiftCode(r.ShiftType?.ShiftCode)) ?? 0,
                IsDeadlineExpired = now > sp.AvailabilityDeadline || currentDate >= sp.WeekStartDate
            };
        }).ToList();

        return new SchedulePeriodIndexViewModel
        {
            Periods = new PagedResult<SchedulePeriodListItemViewModel>(
                items, paged.TotalItems, paged.Page, paged.PageSize)
        };
    }

    /// <inheritdoc />
    public async Task<SchedulePeriodDetailsViewModel?> GetDetailsAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var period = await _schedulePeriodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));

        // BR03: Chỉ hiển thị và xử lý ba ca cố định trong kỳ lịch
        var requirements = period.StaffingRequirements
            .Where(r => IsFixedShiftCode(r.ShiftType?.ShiftCode))
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.ShiftType?.DisplayOrder ?? 0)
            .Select(MapRequirementItem)
            .ToList();

        var workSchedule = period.WorkSchedules.OrderByDescending(w => w.WorkScheduleId).FirstOrDefault();
        var statusResult = SchedulePeriodStatusHelper.CalculateEffectiveStatus(period, workSchedule, now, currentDate);

        var isDeadlineValid = now <= period.AvailabilityDeadline && currentDate < period.WeekStartDate;
        var has21ValidRows = requirements.Count == 21
            && requirements.All(r => r.RequiredSalesStaff >= 0
                && r.RequiredCashiers >= 0
                && r.RequiredSalesStaff + r.RequiredCashiers > 0);

        return new SchedulePeriodDetailsViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            AvailabilityDeadline = _clock.ToVietnamTime(period.AvailabilityDeadline),
            RegistrationStatus = period.RegistrationStatus,
            EffectiveStatusName = statusResult.StatusName,
            NeedsAttention = statusResult.NeedsAttention,
            DisplayBadgeClass = statusResult.DisplayBadgeClass,
            CreatedByUsername = period.CreatedByAccount?.Username ?? string.Empty,
            CreatedAt = _clock.ToVietnamTime(period.CreatedAt),
            Requirements = requirements,
            CanOpenRegistration = period.RegistrationStatus == RegistrationStatus.Preparing
                && isDeadlineValid
                && has21ValidRows
        };
    }

    /// <inheritdoc />
    public async Task<StaffingRequirementEditViewModel?> GetEditStaffingAsync(
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var period = await _schedulePeriodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return null;
        }

        // BR03: Chỉ cho phép chỉnh sửa định mức cho ba ca cố định
        var requirements = period.StaffingRequirements
            .Where(r => IsFixedShiftCode(r.ShiftType?.ShiftCode))
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.ShiftType?.DisplayOrder ?? 0)
            .Select(MapRequirementItem)
            .ToList();

        return new StaffingRequirementEditViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            Requirements = requirements
        };
    }
}
