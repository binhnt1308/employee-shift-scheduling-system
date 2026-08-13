using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.SchedulePeriods.Services;

/// <summary>
/// Service quan ly ky lich va dinh muc nhan su theo FR03/UC03.
/// </summary>
public sealed class SchedulePeriodService : ISchedulePeriodService
{
    private readonly ISchedulePeriodRepository _schedulePeriodRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<SchedulePeriodService> _logger;

    public SchedulePeriodService(
        ISchedulePeriodRepository schedulePeriodRepository,
        IShiftTypeRepository shiftTypeRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<SchedulePeriodService> logger)
    {
        _schedulePeriodRepository = schedulePeriodRepository;
        _shiftTypeRepository = shiftTypeRepository;
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

        var items = paged.Items.Select(sp => new SchedulePeriodListItemViewModel
        {
            SchedulePeriodId = sp.SchedulePeriodId,
            PeriodName = sp.PeriodName,
            WeekStartDate = sp.WeekStartDate,
            WeekEndDate = sp.WeekEndDate,
            AvailabilityDeadline = sp.AvailabilityDeadline,
            RegistrationStatus = sp.RegistrationStatus,
            CreatedByUsername = sp.CreatedByAccount?.Username ?? string.Empty,
            TotalRequirementsCount = sp.StaffingRequirements?.Count(r => IsFixedShiftCode(r.ShiftType?.ShiftCode)) ?? 0,
            IsDeadlineExpired = sp.AvailabilityDeadline <= now
        }).ToList();

        return new SchedulePeriodIndexViewModel
        {
            Periods = new PagedResult<SchedulePeriodListItemViewModel>(items, paged.TotalItems, paged.Page, paged.PageSize)
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

        // BR03: Chi hien thi va xu ly ba ca co dinh trong ky lich
        var requirements = period.StaffingRequirements
            .Where(r => IsFixedShiftCode(r.ShiftType?.ShiftCode))
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.ShiftType?.DisplayOrder ?? 0)
            .Select(MapRequirementItem)
            .ToList();

        var isDeadlineValid = period.AvailabilityDeadline > now;
        var has21ValidRows = requirements.Count == 21
            && requirements.All(r => r.RequiredSalesStaff >= 0 && r.RequiredCashiers >= 0 && r.RequiredSalesStaff + r.RequiredCashiers > 0);

        return new SchedulePeriodDetailsViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            AvailabilityDeadline = period.AvailabilityDeadline,
            RegistrationStatus = period.RegistrationStatus,
            CreatedByUsername = period.CreatedByAccount?.Username ?? string.Empty,
            CreatedAt = period.CreatedAt,
            Requirements = requirements,
            CanOpenRegistration = period.RegistrationStatus == RegistrationStatus.Preparing && isDeadlineValid && has21ValidRows
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

        // BR03: Chi cho phep chinh sua dinh muc cho ba ca co dinh
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

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(
        SchedulePeriodCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        // BR03: Moi ky lich tuong ung voi dung bay ngay lam viec (7 ngay).
        var weekStartDate = model.WeekStartDate;
        var weekEndDate = weekStartDate.AddDays(6);

        if (model.AvailabilityDeadline <= _clock.UtcNow)
        {
            return ServiceResult.Failure(ErrorCodes.InvalidDateRange, "Hạn đăng ký lịch rảnh phải ở tương lai.");
        }

        if (await _schedulePeriodRepository.HasOverlappingPeriodAsync(weekStartDate, weekEndDate, null, cancellationToken))
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodOverlap, "Kỳ lịch trùng thời gian với kỳ lịch đã có.");
        }

        var allShiftTypes = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(allShiftTypes);

        // BR03: He thong phai co va chi su dung dung ba ca co dinh (Morning, Afternoon, Evening)
        if (fixedShifts.Count != 3)
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftTypeNotFound,
                "Hệ thống phải có đủ 3 ca cố định active: Ca sáng (08-12), Ca chiều (12-16), Ca tối (16-20).");
        }

        var periodName = string.IsNullOrWhiteSpace(model.PeriodName)
            ? $"Kỳ lịch {weekStartDate:dd/MM/yyyy} - {weekEndDate:dd/MM/yyyy}"
            : model.PeriodName.Trim();

        var period = new SchedulePeriod
        {
            PeriodName = periodName,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            AvailabilityDeadline = model.AvailabilityDeadline,
            RegistrationStatus = RegistrationStatus.Preparing,
            CreatedByAccountId = actorAccountId,
            CreatedAt = _clock.UtcNow
        };

        _schedulePeriodRepository.Add(period);

        // BR03: Hệ thống chỉ sử dụng ba ca cố định trong kỳ lịch. Tao dung 21 combinations (7 ngay x 3 ca co dinh).
        for (var day = 0; day < 7; day++)
        {
            var workDate = weekStartDate.AddDays(day);
            foreach (var shift in fixedShifts.OrderBy(st => st.DisplayOrder))
            {
                // BR13: Moi ca phai co it nhat mot nhan vien Level2 tro len.
                var requirement = new StaffingRequirement
                {
                    SchedulePeriod = period,
                    WorkDate = workDate,
                    ShiftTypeId = shift.ShiftTypeId,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                };
                _schedulePeriodRepository.AddRequirement(requirement);
            }
        }

        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "CREATE_SCHEDULE_PERIOD",
            nameof(SchedulePeriod),
            periodName,
            null,
            new { weekStartDate, weekEndDate, model.AvailabilityDeadline },
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Schedule period created. ActorAccountId: {ActorAccountId}; WeekStartDate: {WeekStartDate}",
            actorAccountId,
            weekStartDate);

        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateStaffingAsync(
        StaffingRequirementEditViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _schedulePeriodRepository.GetByIdAsync(model.SchedulePeriodId, false, true, cancellationToken);
        if (period is null)
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy kỳ lịch.");
        }

        if (period.RegistrationStatus != RegistrationStatus.Preparing)
        {
            return ServiceResult.Failure(ErrorCodes.InvalidSchedulePeriodStatus, "Chỉ được chỉnh sửa định mức khi kỳ lịch ở trạng thái Chuẩn bị.");
        }

        var allShiftTypes = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(allShiftTypes);
        var validFixedShiftIds = fixedShifts.Select(st => st.ShiftTypeId).ToHashSet();

        foreach (var item in model.Requirements)
        {
            if (item.WorkDate < period.WeekStartDate || item.WorkDate > period.WeekEndDate)
            {
                return ServiceResult.Failure(ErrorCodes.InvalidDateRange, $"Ngày làm việc {item.WorkDate:dd/MM/yyyy} nằm ngoài kỳ lịch.");
            }

            // BR03: Hệ thống chỉ sử dụng ba ca cố định trong kỳ lịch. Tu choi ShiftType ngoai 3 ca co dinh (vi du ca thu 4).
            if (!validFixedShiftIds.Contains(item.ShiftTypeId))
            {
                return ServiceResult.Failure(ErrorCodes.ShiftTypeNotFound, "Ca làm việc không thuộc 3 ca cố định của kỳ lịch.");
            }

            // BR12: Mỗi ca phải có định mức nhân sự hợp lệ theo từng vị trí.
            if (item.RequiredSalesStaff < 0)
            {
                return ServiceResult.Failure(ErrorCodes.InvalidStaffingRequirement, "Số lượng nhân viên bán hàng không được âm.");
            }

            if (item.RequiredCashiers < 0)
            {
                return ServiceResult.Failure(ErrorCodes.InvalidStaffingRequirement, "Số lượng nhân viên thu ngân không được âm.");
            }

            if (item.RequiredSalesStaff + item.RequiredCashiers <= 0)
            {
                return ServiceResult.Failure(ErrorCodes.InvalidStaffingRequirement, $"Tổng nhân sự cho ngày {item.WorkDate:dd/MM/yyyy} ({item.ShiftName}) phải lớn hơn 0.");
            }
        }

        foreach (var item in model.Requirements)
        {
            var req = period.StaffingRequirements.FirstOrDefault(r => r.StaffingRequirementId == item.StaffingRequirementId
                                                                       || (r.WorkDate == item.WorkDate && r.ShiftTypeId == item.ShiftTypeId));
            if (req is not null)
            {
                req.RequiredSalesStaff = item.RequiredSalesStaff;
                req.RequiredCashiers = item.RequiredCashiers;
                // BR13: Mỗi ca phải yêu cầu ít nhất một nhân viên từ Level 2 trở lên.
                req.RequiredExperiencedStaff = 1;
                req.MinimumLevel = EmployeeLevel.Level2;
            }
        }

        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "UPDATE_STAFFING_REQUIREMENTS",
            nameof(SchedulePeriod),
            period.SchedulePeriodId.ToString(),
            null,
            new { period.SchedulePeriodId, ItemCount = model.Requirements.Count },
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Staffing requirements updated. ActorAccountId: {ActorAccountId}; SchedulePeriodId: {SchedulePeriodId}",
            actorAccountId,
            period.SchedulePeriodId);

        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> OpenRegistrationAsync(
        int schedulePeriodId,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _schedulePeriodRepository.GetByIdAsync(schedulePeriodId, false, true, cancellationToken);
        if (period is null)
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy kỳ lịch.");
        }

        if (period.RegistrationStatus != RegistrationStatus.Preparing)
        {
            return ServiceResult.Failure(ErrorCodes.InvalidSchedulePeriodStatus, "Kỳ lịch không ở trạng thái Chuẩn bị.");
        }

        // BR04: Kỳ lịch chỉ mở đăng ký khi thời hạn đăng ký còn hiệu lực.
        if (period.AvailabilityDeadline <= _clock.UtcNow)
        {
            return ServiceResult.Failure(ErrorCodes.RegistrationDeadlineExpired, "Thời hạn đăng ký lịch rảnh đã hết. Không thể mở đăng ký.");
        }

        var allShiftTypes = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(allShiftTypes);
        var validFixedShiftIds = fixedShifts.Select(st => st.ShiftTypeId).ToHashSet();

        // BR03: Hệ thống chỉ sử dụng ba ca cố định trong kỳ lịch. Revalidate exact 21 combinations (7 days x 3 fixed shifts).
        var requirements = period.StaffingRequirements.ToList();
        var validFixedRequirements = requirements.Where(r => validFixedShiftIds.Contains(r.ShiftTypeId)).ToList();

        if (validFixedRequirements.Count != 21 || requirements.Count != 21)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidStaffingRequirement,
                "Kỳ lịch phải có chính xác đúng 21 định mức ca (7 ngày x 3 ca cố định).");
        }

        // Kiem tra du 7 ngay x 3 ca co dinh duy nhat
        var combinations = new HashSet<(DateOnly WorkDate, int ShiftTypeId)>();
        for (var day = 0; day < 7; day++)
        {
            var date = period.WeekStartDate.AddDays(day);
            foreach (var shiftId in validFixedShiftIds)
            {
                combinations.Add((date, shiftId));
            }
        }

        foreach (var req in requirements)
        {
            if (req.WorkDate < period.WeekStartDate || req.WorkDate > period.WeekEndDate)
            {
                return ServiceResult.Failure(ErrorCodes.InvalidDateRange, $"Ngày làm việc {req.WorkDate:dd/MM/yyyy} nằm ngoài kỳ lịch.");
            }

            if (!combinations.Remove((req.WorkDate, req.ShiftTypeId)))
            {
                return ServiceResult.Failure(ErrorCodes.InvalidStaffingRequirement, "Định mức ca bị trùng lặp hoặc chứa ca thứ 4 không hợp lệ.");
            }

            // BR12: Mỗi ca phải có định mức nhân sự hợp lệ theo từng vị trí.
            if (req.RequiredSalesStaff < 0 || req.RequiredCashiers < 0 || (req.RequiredSalesStaff + req.RequiredCashiers) <= 0)
            {
                return ServiceResult.Failure(
                    ErrorCodes.InvalidStaffingRequirement,
                    $"Định mức nhân sự cho ngày {req.WorkDate:dd/MM/yyyy} chưa hợp lệ (tổng Bán hàng + Thu ngân phải lớn hơn 0).");
            }

            // BR13: Mỗi ca phải yêu cầu ít nhất một nhân viên từ Level 2 trở lên.
            if (req.RequiredExperiencedStaff < 1 || req.MinimumLevel < EmployeeLevel.Level2)
            {
                return ServiceResult.Failure(
                    ErrorCodes.InvalidStaffingRequirement,
                    $"Mỗi ca làm việc phải yêu cầu ít nhất 1 nhân viên kinh nghiệm (từ Level 2 trở lên).");
            }
        }

        if (combinations.Count > 0)
        {
            return ServiceResult.Failure(ErrorCodes.InvalidStaffingRequirement, "Thiếu định mức cho một số ca trong 21 ca cố định.");
        }

        period.RegistrationStatus = RegistrationStatus.Open;

        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "OPEN_PERIOD_REGISTRATION",
            nameof(SchedulePeriod),
            period.SchedulePeriodId.ToString(),
            new { RegistrationStatus = RegistrationStatus.Preparing },
            new { RegistrationStatus = RegistrationStatus.Open },
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registration opened for schedule period. ActorAccountId: {ActorAccountId}; SchedulePeriodId: {SchedulePeriodId}",
            actorAccountId,
            schedulePeriodId);

        return ServiceResult.Success();
    }

    private static List<ShiftType> GetFixedThreeShifts(IReadOnlyList<ShiftType> allShiftTypes)
    {
        var morning = allShiftTypes.FirstOrDefault(st => st.IsActive
            && ShiftTypeConstants.IsValidFixedShiftTime(st.ShiftCode, st.StartTime, st.EndTime)
            && st.ShiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase));

        var afternoon = allShiftTypes.FirstOrDefault(st => st.IsActive
            && ShiftTypeConstants.IsValidFixedShiftTime(st.ShiftCode, st.StartTime, st.EndTime)
            && st.ShiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase));

        var evening = allShiftTypes.FirstOrDefault(st => st.IsActive
            && ShiftTypeConstants.IsValidFixedShiftTime(st.ShiftCode, st.StartTime, st.EndTime)
            && st.ShiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase));

        if (morning is null || afternoon is null || evening is null)
        {
            return new List<ShiftType>();
        }

        return new List<ShiftType> { morning, afternoon, evening };
    }

    private static bool IsFixedShiftCode(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode))
        {
            return false;
        }

        return shiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase);
    }

    private static StaffingRequirementItemViewModel MapRequirementItem(StaffingRequirement req)
    {
        return new StaffingRequirementItemViewModel
        {
            StaffingRequirementId = req.StaffingRequirementId,
            WorkDate = req.WorkDate,
            ShiftTypeId = req.ShiftTypeId,
            ShiftCode = req.ShiftType?.ShiftCode ?? string.Empty,
            ShiftName = req.ShiftType?.ShiftName ?? string.Empty,
            StartTime = req.ShiftType?.StartTime ?? default,
            EndTime = req.ShiftType?.EndTime ?? default,
            RequiredSalesStaff = req.RequiredSalesStaff,
            RequiredCashiers = req.RequiredCashiers
        };
    }
}
