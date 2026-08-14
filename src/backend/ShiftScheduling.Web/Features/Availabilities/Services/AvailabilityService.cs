using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Availabilities.Services;

/// <summary>
/// Service quan ly va tra cuu lich ranh nhan vien theo UC04/BR03/BR04/BR05.
/// </summary>
public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IAvailabilityRepository _availabilityRepository;
    private readonly ISchedulePeriodRepository _periodRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AvailabilityService> _logger;

    public AvailabilityService(
        IAvailabilityRepository availabilityRepository,
        ISchedulePeriodRepository periodRepository,
        IShiftTypeRepository shiftTypeRepository,
        IEmployeeRepository employeeRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AvailabilityService> logger)
    {
        _availabilityRepository = availabilityRepository;
        _periodRepository = periodRepository;
        _shiftTypeRepository = shiftTypeRepository;
        _employeeRepository = employeeRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<AvailabilityPeriodItemViewModel>> GetEmployeePeriodsAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return new List<AvailabilityPeriodItemViewModel>();
        }

        var periodsResult = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
        var now = _clock.UtcNow;

        var result = new List<AvailabilityPeriodItemViewModel>();
        foreach (var period in periodsResult.Items)
        {
            var avails = await _availabilityRepository.GetByEmployeeAndPeriodAsync(
                employee.EmployeeId, period.SchedulePeriodId, cancellationToken);

            var isOpen = period.RegistrationStatus == RegistrationStatus.Open;
            var isExpired = period.AvailabilityDeadline <= now;

            result.Add(new AvailabilityPeriodItemViewModel
            {
                SchedulePeriodId = period.SchedulePeriodId,
                PeriodName = period.PeriodName,
                WeekStartDate = period.WeekStartDate,
                WeekEndDate = period.WeekEndDate,
                AvailabilityDeadline = period.AvailabilityDeadline,
                RegistrationStatus = period.RegistrationStatus,
                IsOpen = isOpen && !isExpired,
                IsExpired = isExpired,
                RegisteredCount = avails.Count
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AvailabilityRegisterViewModel?> GetEmployeeRegisterFormAsync(
        int accountId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(allShifts);

        var existing = await _availabilityRepository.GetByEmployeeAndPeriodAsync(
            employee.EmployeeId, schedulePeriodId, cancellationToken);
        var selectedSet = existing.Select(a => $"{a.WorkDate:yyyy-MM-dd}_{a.ShiftTypeId}").ToHashSet();

        var now = _clock.UtcNow;
        var isOpen = period.RegistrationStatus == RegistrationStatus.Open;
        var isDeadlineValid = period.AvailabilityDeadline > now;
        var isEditable = isOpen && isDeadlineValid;

        string? readonlyReason = null;
        if (!isOpen)
        {
            readonlyReason = "Kỳ lịch chưa mở đăng ký hoặc đã đóng đăng ký.";
        }
        else if (!isDeadlineValid)
        {
            readonlyReason = "Thời hạn đăng ký lịch rảnh đã hết.";
        }

        var days = new List<AvailabilityDayViewModel>();
        var viCulture = new CultureInfo("vi-VN");

        for (int i = 0; i < 7; i++)
        {
            var date = period.WeekStartDate.AddDays(i);
            var dayName = date.ToString("dddd (dd/MM)", viCulture);

            var dayShifts = fixedShifts.Select(s => new AvailabilityCellViewModel
            {
                WorkDate = date,
                ShiftTypeId = s.ShiftTypeId,
                ShiftCode = s.ShiftCode,
                ShiftName = s.ShiftName,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                IsSelected = selectedSet.Contains($"{date:yyyy-MM-dd}_{s.ShiftTypeId}")
            }).ToList();

            days.Add(new AvailabilityDayViewModel
            {
                WorkDate = date,
                DayOfWeekName = dayName,
                Shifts = dayShifts
            });
        }

        return new AvailabilityRegisterViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            AvailabilityDeadline = period.AvailabilityDeadline,
            IsEditable = isEditable,
            ReadonlyReason = readonlyReason,
            Days = days,
            SelectedShiftKeys = selectedSet.ToList()
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SaveEmployeeAvailabilityAsync(
        int accountId,
        int schedulePeriodId,
        List<string> selectedShiftKeys,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotFound, "Không tìm thấy hồ sơ nhân viên.");
        }

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null)
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy kỳ lịch.");
        }

        // BR04: Nhân viên chỉ được đăng ký khi kỳ lịch đang mở và chưa hết thời hạn
        var now = _clock.UtcNow;
        if (period.RegistrationStatus != RegistrationStatus.Open)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidSchedulePeriodStatus,
                "Kỳ lịch hiện không mở đăng ký lịch rảnh.");
        }

        if (period.AvailabilityDeadline <= now)
        {
            return ServiceResult.Failure(
            ErrorCodes.RegistrationDeadlineExpired,
            "Đã hết thời hạn đăng ký lịch rảnh.");
        }

        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var validShiftIds = GetFixedThreeShifts(allShifts).Select(s => s.ShiftTypeId).ToHashSet();

        var parsedShifts = new List<(DateOnly WorkDate, int ShiftTypeId)>();
        foreach (var key in selectedShiftKeys ?? new List<string>())
        {
            var parts = key.Split('_');
            if (parts.Length == 2
            && DateOnly.TryParse(parts[0], out var date)
            && int.TryParse(parts[1], out var shiftId))
            {
                // BR03: Chỉ đăng ký theo 3 ca cố định và thuộc tuần kỳ lịch
                if (date >= period.WeekStartDate && date <= period.WeekEndDate && validShiftIds.Contains(shiftId))
                {
                    parsedShifts.Add((date, shiftId));
                }
            }
        }

        await _availabilityRepository.SaveEmployeeAvailabilitiesAsync(
            employee.EmployeeId, schedulePeriodId, parsedShifts, now, cancellationToken);

        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "SAVE_EMPLOYEE_AVAILABILITY",
            nameof(Availability),
            $"{employee.EmployeeCode}_{period.SchedulePeriodId}",
            null,
            new { schedulePeriodId, Count = parsedShifts.Count },
            now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee availability saved. EmployeeCode: {EmployeeCode}; PeriodId: {PeriodId}; Count: {Count}",
            employee.EmployeeCode, schedulePeriodId, parsedShifts.Count);

        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ManagerAvailabilityOverviewViewModel?> GetManagerOverviewAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        DateOnly? dateFilter = null,
        int? shiftTypeIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null)
        {
            var allPeriods = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
            period = allPeriods.Items.OrderByDescending(p => p.SchedulePeriodId).FirstOrDefault();
            if (period is null) return null;
            schedulePeriodId = period.SchedulePeriodId;
        }

        var allPeriodsResult = await _periodRepository.GetPagedAsync(1, 100, cancellationToken);
        var periodOptions = allPeriodsResult.Items.Select(p => new SchedulePeriodOptionViewModel
        {
            SchedulePeriodId = p.SchedulePeriodId,
            PeriodName = p.PeriodName
        }).ToList();

        var employees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var employeeOptions = employees.Select(e => new EmployeeOptionViewModel
        {
            EmployeeId = e.EmployeeId,
            DisplayName = $"{e.EmployeeCode} - {e.FullName}"
        }).ToList();

        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(allShifts);
        var shiftOptions = fixedShifts.Select(s => new ShiftTypeOptionViewModel
        {
            ShiftTypeId = s.ShiftTypeId,
            ShiftName = s.ShiftName
        }).ToList();

        var avails = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, employeeIdFilter, positionFilter, dateFilter, shiftTypeIdFilter, cancellationToken);

        var viCulture = new CultureInfo("vi-VN");
        var days = new List<AvailabilityDayOverviewViewModel>();

        for (int i = 0; i < 7; i++)
        {
            var date = period.WeekStartDate.AddDays(i);
            if (dateFilter.HasValue && dateFilter.Value != date)
            {
                continue;
            }

            var dayName = date.ToString("dddd (dd/MM)", viCulture);
            var shiftCells = new List<ManagerAvailabilityCellViewModel>();

            foreach (var shift in fixedShifts)
            {
                if (shiftTypeIdFilter.HasValue
                    && shiftTypeIdFilter.Value > 0
                    && shift.ShiftTypeId != shiftTypeIdFilter.Value)
                {
                    continue;
                }

                var shiftAvails = avails.Where(a => a.WorkDate == date && a.ShiftTypeId == shift.ShiftTypeId).ToList();
                var empItems = shiftAvails.Select(a => new ManagerAvailabilityEmployeeItem
                {
                    EmployeeId = a.Employee.EmployeeId,
                    EmployeeCode = a.Employee.EmployeeCode,
                    FullName = a.Employee.FullName,
                    Position = a.Employee.Position,
                    EmploymentType = a.Employee.EmploymentType,
                    Level = a.Employee.Level
                }).OrderBy(e => e.Position).ThenBy(e => e.FullName).ToList();

                shiftCells.Add(new ManagerAvailabilityCellViewModel
                {
                    WorkDate = date,
                    ShiftTypeId = shift.ShiftTypeId,
                    ShiftCode = shift.ShiftCode,
                    ShiftName = shift.ShiftName,
                    AvailableEmployees = empItems
                });
            }

            days.Add(new AvailabilityDayOverviewViewModel
            {
                WorkDate = date,
                DayOfWeekName = dayName,
                Shifts = shiftCells
            });
        }

        return new ManagerAvailabilityOverviewViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            EmployeeIdFilter = employeeIdFilter,
            PositionFilter = positionFilter,
            DateFilter = dateFilter,
            ShiftTypeIdFilter = shiftTypeIdFilter,
            PeriodOptions = periodOptions,
            EmployeeOptions = employeeOptions,
            ShiftOptions = shiftOptions,
            Days = days
        };
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
}
