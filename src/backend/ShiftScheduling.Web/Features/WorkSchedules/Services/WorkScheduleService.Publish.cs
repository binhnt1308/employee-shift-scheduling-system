using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.WorkSchedules.Services;

public partial class WorkScheduleService
{
    /// <inheritdoc />
    public async Task<ServiceResult> PublishScheduleAsync(
        int schedulePeriodId,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        if (period is null)
        {
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy kỳ lịch.");
        }

        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, false, true, cancellationToken);

        if (schedule is null)
        {
            return ServiceResult.Failure(
                ErrorCodes.WorkScheduleNotFound,
                "Không tìm thấy lịch nháp để công bố.");
        }

        if (schedule.Status == ScheduleStatus.Published)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus,
                "Kỳ lịch này đã được công bố trước đó.");
        }

        // BR16: Tải dữ liệu mới nhất từ DB và kiểm tra lại toàn bộ các ràng buộc
        // ngay trước khi công bố
        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);

        // BR02: Khong cong bo lich neu con phan cong cho nhan vien da ngung hoat dong.
        var activeEmployeeIds = activeEmployees.Select(e => e.EmployeeId).ToHashSet();
        if (schedule.ShiftAssignments.Any(a => !activeEmployeeIds.Contains(a.EmployeeId)))
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidSchedulePublish,
                "Lịch làm việc vẫn còn phân công cho nhân viên đã ngừng hoạt động (BR02).");
        }
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, null, null, cancellationToken);
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var periodShifts = period.StaffingRequirements
            .Select(r => r.ShiftType)
            .Where(st => st != null)
            .DistinctBy(st => st.ShiftTypeId)
            .OrderBy(st => st.DisplayOrder)
            .ToList();
        var shiftsForValidation = periodShifts.Count == 3 ? periodShifts : allShifts;

        var validationResult = _validationService.ValidateSchedule(
            period, activeEmployees, availabilities, leaves, period.StaffingRequirements.ToList(),
            schedule.ShiftAssignments.ToList(), shiftsForValidation);

        // BR14: Lịch còn vi phạm ràng buộc bắt buộc không được phép công bố
        if (!validationResult.IsValid)
        {
            var errorSummary = string.Join("; ", validationResult.HardErrors.Take(3));
            return ServiceResult.Failure(
                ErrorCodes.InvalidSchedulePublish,
                $"Lịch làm việc vẫn còn {validationResult.HardErrors.Count}" +
                $" vi phạm bắt buộc. Không thể công bố: {errorSummary}");
        }

        var now = _clock.UtcNow;
        await _workScheduleRepository.PublishScheduleAsync(schedule.WorkScheduleId, accountId, now, cancellationToken);

        // Tạo thông báo cho các nhân viên có ca làm việc trong lịch vừa công bố
        var assignedEmpIds = schedule.ShiftAssignments.Select(a => a.EmployeeId).Distinct().ToList();
        var empAccountMap = activeEmployees
            .Where(e => e.UserAccountId.HasValue && assignedEmpIds.Contains(e.EmployeeId))
            .ToDictionary(e => e.EmployeeId, e => e.UserAccountId!.Value);

        foreach (var empId in assignedEmpIds)
        {
            if (empAccountMap.TryGetValue(empId, out var empAccountId))
            {
                _notificationRepository.Add(new Notification
                {
                    ReceiverAccountId = empAccountId,
                    Title = "Lịch làm việc mới đã được công bố",
                    Content = $"Quản lý đã công bố chính thức lịch làm việc cho {period.PeriodName}.",
                    Type = NotificationType.SchedulePublished,
                    RelatedObjectId = period.SchedulePeriodId,
                    IsRead = false,
                    CreatedAt = now
                });
            }
        }

        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "PUBLISH_WORK_SCHEDULE",
            nameof(WorkSchedule),
            schedule.WorkScheduleId.ToString(),
            new { Status = ScheduleStatus.Draft },
            new { Status = ScheduleStatus.Published, PublishedAt = now },
            now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_emailSender is not null)
        {
            var assignedEmployees = activeEmployees
                .Where(e => assignedEmpIds.Contains(e.EmployeeId) && !string.IsNullOrWhiteSpace(e.Email))
                .DistinctBy(e => e.Email!.Trim().ToLowerInvariant())
                .ToList();

            var subject = "Lịch làm việc đã được công bố";
            var startFormatted = period.WeekStartDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var endFormatted = period.WeekEndDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var body = $"Quản lý đã công bố chính thức lịch làm việc cho {period.PeriodName} ({startFormatted} - {endFormatted}). Vui lòng đăng nhập hệ thống để xem chi tiết lịch làm việc của bạn.";

            foreach (var emp in assignedEmployees)
            {
                try
                {
                    await _emailSender.SendAsync(emp.Email!.Trim(), subject, body, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to send schedule published email to {Email} for Period {PeriodId}",
                        emp.Email,
                        schedulePeriodId);
                }
            }
        }

        _logger.LogInformation(
            "Schedule published successfully. ActorAccountId: {ActorAccountId}; PeriodId: {PeriodId}",
            accountId, schedulePeriodId);

        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CloseScheduleAsync(
        int schedulePeriodId,
        string? rowVersion,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        var (isValidRowVersion, expectedRowVersion) = ParseRowVersion(rowVersion);
        if (!isValidRowVersion)
        {
            return ServiceResult.Failure(
                ErrorCodes.ConcurrencyConflict,
                "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.");
        }

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, true, cancellationToken);
        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            schedulePeriodId, false, true, cancellationToken);

        var eligibilityError = ValidateCloseEligibility(period, schedule, _clock.TodayInVietnam);
        if (eligibilityError is not null)
        {
            return eligibilityError;
        }

        if (schedule!.RowVersion is not null && expectedRowVersion.Length > 0 && !schedule.RowVersion.SequenceEqual(expectedRowVersion))
        {
            return ServiceResult.Failure(
                ErrorCodes.ConcurrencyConflict,
                "Dữ liệu lịch đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang.");
        }

        var now = _clock.UtcNow;
        var prevValues = new { schedule!.WorkScheduleId, schedule.Status, schedule.UpdatedAt };
        schedule.Status = ScheduleStatus.Closed;
        schedule.UpdatedAt = now;

        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "CLOSE_WORK_SCHEDULE",
            nameof(WorkSchedule),
            schedule.WorkScheduleId.ToString(),
            prevValues,
            new { schedule.WorkScheduleId, Status = ScheduleStatus.Closed, UpdatedAt = now },
            now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success("Đã chốt kỳ làm việc thành công.");
    }

    private static (bool IsValid, byte[] RowVersion) ParseRowVersion(string? rowVersion)
    {
        try
        {
            var bytes = string.IsNullOrWhiteSpace(rowVersion) ? [] : Convert.FromBase64String(rowVersion);
            return (bytes.Length > 0, bytes);
        }
        catch (FormatException)
        {
            return (false, []);
        }
    }

    private static ServiceResult? ValidateCloseEligibility(
        SchedulePeriod? period,
        WorkSchedule? schedule,
        DateOnly currentDate)
    {
        if (period is null)
            return ServiceResult.Failure(ErrorCodes.SchedulePeriodNotFound, "Không tìm thấy thông tin kỳ lịch.");
        if (schedule is null)
            return ServiceResult.Failure(ErrorCodes.WorkScheduleNotFound, "Không tìm thấy lịch làm việc của kỳ.");
        if (schedule.Status == ScheduleStatus.Closed)
            return ServiceResult.Failure(ErrorCodes.InvalidScheduleStatus, "Kỳ lịch làm việc đã được chốt từ trước.");
        if (schedule.Status != ScheduleStatus.Published)
            return ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Chỉ có thể chốt kỳ làm việc sau khi lịch đã được công bố.");

        // QT-6: Chỉ cho phép chốt kỳ sau khi kỳ làm việc đã kết thúc (dùng quy tắc tập trung)
        if (!SchedulePeriodStatusHelper.CanCloseSchedule(period, schedule, currentDate))
            return ServiceResult.Failure(
                ErrorCodes.InvalidScheduleStatus, "Chỉ có thể chốt kỳ sau khi kỳ làm việc kết thúc.");

        return null;
    }

    /// <inheritdoc />
    public async Task<WorkScheduleEditViewModel?> GetEmployeeMyScheduleAsync(
        int accountId,
        int? schedulePeriodId = null,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var publishedSchedule = await ResolveEmployeeScheduleAsync(schedulePeriodId, cancellationToken);
        if (publishedSchedule is null)
        {
            return null;
        }

        var period = publishedSchedule.SchedulePeriod;
        var fixedShifts = await ResolveFixedShiftsAsync(period, cancellationToken);

        // Chi loc nhung ca cua chinh nhan vien dang dang nhap (UC08)
        var myAssignments = publishedSchedule.ShiftAssignments
            .Where(a => a.EmployeeId == employee.EmployeeId)
            .ToList();

        // Dong bo voi man Manager: nap don nghi da duyet cua ky lich de hien thi dung trang thai nghi
        var approvedLeaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            period.SchedulePeriodId, cancellationToken);

        var days = BuildGridDays(
            period, fixedShifts, new List<StaffingRequirement>(), myAssignments, new List<Employee> { employee }, null, approvedLeaves);

        var now = _clock.UtcNow;
        var currentDate = DateOnly.FromDateTime(_clock.ToVietnamTime(now));
        var statusResult = SchedulePeriodStatusHelper.CalculateEffectiveStatus(
            period, publishedSchedule, now, currentDate);

        var periodOptions = await BuildPeriodOptionsAsync(now, currentDate, cancellationToken);

        return BuildMyScheduleViewModel(period, publishedSchedule, statusResult, days, periodOptions);
    }

    private static WorkScheduleEditViewModel BuildMyScheduleViewModel(
        SchedulePeriod period,
        WorkSchedule schedule,
        SchedulePeriodStatusHelper.EffectiveStatusResult statusResult,
        List<SchedulingDayGridViewModel> days,
        List<MySchedulePeriodOptionViewModel> periodOptions)
    {
        return new WorkScheduleEditViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            RegistrationStatus = period.RegistrationStatus,
            WorkScheduleId = schedule.WorkScheduleId,
            Status = schedule.Status,
            EffectiveStatusName = statusResult.StatusName,
            DisplayBadgeClass = statusResult.DisplayBadgeClass,
            IsGeneratedByORTools = schedule.IsGeneratedByORTools,
            Days = days,
            ValidationResult = new ScheduleValidationResultViewModel { IsValid = true },
            PeriodOptions = periodOptions
        };
    }

    private async Task<WorkSchedule?> ResolveEmployeeScheduleAsync(
        int? schedulePeriodId,
        CancellationToken cancellationToken)
    {
        if (schedulePeriodId.HasValue && schedulePeriodId.Value > 0)
        {
            var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
                schedulePeriodId.Value, true, true, cancellationToken);
            if (schedule is not null && schedule.Status != ScheduleStatus.Published && schedule.Status != ScheduleStatus.Closed)
            {
                return null;
            }

            return schedule;
        }

        return await _workScheduleRepository.GetDefaultPublishedScheduleAsync(
            _clock.TodayInVietnam, cancellationToken);
    }

    private async Task<List<ShiftType>> ResolveFixedShiftsAsync(
        SchedulePeriod period,
        CancellationToken cancellationToken)
    {
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var periodShifts = period.StaffingRequirements
            .Select(r => r.ShiftType)
            .Where(st => st != null)
            .DistinctBy(st => st.ShiftTypeId)
            .OrderBy(st => st.DisplayOrder)
            .ToList();

        return periodShifts.Count == 3 ? periodShifts : GetFixedThreeShifts(allShifts);
    }

    private async Task<List<MySchedulePeriodOptionViewModel>> BuildPeriodOptionsAsync(
        DateTime now,
        DateOnly currentDate,
        CancellationToken cancellationToken)
    {
        var allPeriods = await _periodRepository.GetAllWithSchedulesAsync(cancellationToken);
        var periodOptions = new List<MySchedulePeriodOptionViewModel>();
        foreach (var p in allPeriods)
        {
            var sched = p.WorkSchedules.FirstOrDefault(ws => ws.Status == ScheduleStatus.Published || ws.Status == ScheduleStatus.Closed);
            if (sched is null)
            {
                continue;
            }

            var statusRes = SchedulePeriodStatusHelper.CalculateEffectiveStatus(p, sched, now, currentDate);
            periodOptions.Add(new MySchedulePeriodOptionViewModel
            {
                SchedulePeriodId = p.SchedulePeriodId,
                PeriodName = p.PeriodName,
                WeekStartDate = p.WeekStartDate,
                WeekEndDate = p.WeekEndDate,
                Status = sched.Status,
                EffectiveStatusName = statusRes.StatusName,
                DisplayText = $"{p.WeekStartDate:dd/MM/yyyy} - {p.WeekEndDate:dd/MM/yyyy} ({statusRes.StatusName})"
            });
        }

        return periodOptions;
    }
}
