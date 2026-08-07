using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Notifications;
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
            return ServiceResult.Failure(ErrorCodes.WorkScheduleNotFound, "Không tìm thấy lịch nháp để công bố.");
        }

        if (schedule.Status == ScheduleStatus.Published)
        {
            return ServiceResult.Failure(ErrorCodes.InvalidScheduleStatus, "Kỳ lịch này đã được công bố trước đó.");
        }

        // BR16: Tải dữ liệu mới nhất từ DB và kiểm tra lại toàn bộ các ràng buộc ngay trước khi công bố
        var activeEmployees = await _employeeRepository.GetAllActiveAsync(cancellationToken);
        var availabilities = await _availabilityRepository.GetByPeriodWithDetailsAsync(
            schedulePeriodId, null, null, null, null, cancellationToken);
        var leaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);

        var validationResult = _validationService.ValidateSchedule(
            period, activeEmployees, availabilities, leaves, period.StaffingRequirements.ToList(),
            schedule.ShiftAssignments.ToList(), allShifts);

        // BR14: Lịch còn vi phạm ràng buộc bắt buộc không được phép công bố
        if (!validationResult.IsValid)
        {
            var errorSummary = string.Join("; ", validationResult.HardErrors.Take(3));
            return ServiceResult.Failure(
                ErrorCodes.InvalidSchedulePublish,
                $"Lịch làm việc vẫn còn {validationResult.HardErrors.Count} vi phạm bắt buộc. Không thể công bố: {errorSummary}");
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

        _logger.LogInformation(
            "Schedule published successfully. ActorAccountId: {ActorAccountId}; PeriodId: {PeriodId}",
            accountId, schedulePeriodId);

        return ServiceResult.Success();
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

        WorkSchedule? publishedSchedule = null;

        if (schedulePeriodId.HasValue && schedulePeriodId.Value > 0)
        {
            publishedSchedule = await _workScheduleRepository.GetPublishedByPeriodIdAsync(
                schedulePeriodId.Value, cancellationToken);
        }
        else
        {
            var pagedPeriods = await _periodRepository.GetPagedAsync(1, 50, cancellationToken);
            foreach (var p in pagedPeriods.Items)
            {
                var sch = await _workScheduleRepository.GetPublishedByPeriodIdAsync(
                    p.SchedulePeriodId, cancellationToken);
                if (sch is not null)
                {
                    publishedSchedule = sch;
                    break;
                }
            }
        }

        if (publishedSchedule is null)
        {
            return null;
        }

        var period = publishedSchedule.SchedulePeriod;
        var allShifts = await _shiftTypeRepository.GetAllAsync(true, cancellationToken);
        var fixedShifts = GetFixedThreeShifts(allShifts);

        // Chi loc nhung ca cua chinh nhan vien dang dang nhap (UC08)
        var myAssignments = publishedSchedule.ShiftAssignments
            .Where(a => a.EmployeeId == employee.EmployeeId)
            .ToList();

        var days = BuildGridDays(
            period, fixedShifts, new List<StaffingRequirement>(), myAssignments, new List<Employee> { employee });

        return new WorkScheduleEditViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            RegistrationStatus = period.RegistrationStatus,
            WorkScheduleId = publishedSchedule.WorkScheduleId,
            Status = publishedSchedule.Status,
            IsGeneratedByORTools = publishedSchedule.IsGeneratedByORTools,
            Days = days,
            ValidationResult = new ScheduleValidationResultViewModel { IsValid = true }
        };
    }
}
