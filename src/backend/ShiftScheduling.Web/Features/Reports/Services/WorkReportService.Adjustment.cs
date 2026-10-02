using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.Reports.Services;

public sealed partial class WorkReportService
{
    /// <inheritdoc />
    public async Task<ServiceResult> AdjustWorkdayHoursAsync(
        int actorAccountId,
        AdjustWorkdayHoursRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ShiftAssignmentId <= 0)
        {
            return ServiceResult.Failure("InvalidShiftAssignment", "Mã phân công không hợp lệ.");
        }

        var schedule = await _workScheduleRepository.GetByPeriodIdAsync(
            request.SchedulePeriodId,
            asNoTracking: false,
            includeAssignments: true,
            cancellationToken);

        if (schedule == null)
        {
            return ServiceResult.Failure("ScheduleNotFound", "Không tìm thấy lịch làm việc của kỳ.");
        }

        if (schedule.Status == ScheduleStatus.Closed)
        {
            return ServiceResult.Failure("ScheduleClosed", "Kỳ lịch làm việc đã chốt, không thể điều chỉnh giờ công.");
        }

        if (schedule.Status != ScheduleStatus.Published)
        {
            return ServiceResult.Failure("ScheduleNotPublished", "Chỉ có thể điều chỉnh ngày công trên lịch làm việc đã công bố.");
        }

        var assignment = schedule.ShiftAssignments
            .FirstOrDefault(a => a.ShiftAssignmentId == request.ShiftAssignmentId);

        if (assignment == null)
        {
            return ServiceResult.Failure("ShiftAssignmentNotFound", "Không tìm thấy phân công ca làm việc trong kỳ lịch.");
        }

        if (assignment.ShiftType == null)
        {
            return ServiceResult.Failure("ShiftTypeNotFound", "Không tìm thấy thông tin loại ca.");
        }

        // Ca qua đêm kết thúc ở ngày kế tiếp; phải ghép đúng ngày + giờ kết thúc trước khi cho phép điều chỉnh.
        var vietnamNow = _clock.ToVietnamTime(_clock.UtcNow);
        var shiftEndDate = assignment.WorkDate;
        if (assignment.ShiftType.EndTime <= assignment.ShiftType.StartTime)
        {
            shiftEndDate = shiftEndDate.AddDays(1);
        }
        var shiftEndDateTime = shiftEndDate.ToDateTime(assignment.ShiftType.EndTime);
        if (vietnamNow < shiftEndDateTime)
        {
            return ServiceResult.Failure("ShiftNotEnded", "Chỉ được điều chỉnh giờ công sau khi ca làm việc đã kết thúc.");
        }

        var approvedLeaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            request.SchedulePeriodId, cancellationToken);
        var leaveLookup = new ApprovedLeaveLookup(approvedLeaves, request.SchedulePeriodId);

        bool hasApprovedLeave = leaveLookup.HasApprovedLeave(
            assignment.EmployeeId, request.SchedulePeriodId, assignment.WorkDate, assignment.ShiftTypeId);

        // Giờ hệ thống mặc định bằng thời lượng ca, riêng ca có nghỉ đã duyệt được tính 0 giờ.
        // ActualWorkedHours chỉ ghi khi Quản lý thực sự điều chỉnh khác giá trị mặc định này.
        decimal systemCalculatedHours = hasApprovedLeave ? 0m : assignment.ShiftType.DurationHours;
        decimal currentEffectiveHours = assignment.ActualWorkedHours ?? systemCalculatedHours;

        var prevValues = new
        {
            assignment.ActualWorkedHours,
            assignment.WorkdayAdjustmentReason,
            EffectiveWorkedHours = currentEffectiveHours
        };

        // Đặt lại sẽ xóa điều chỉnh thủ công để báo cáo quay về cách tính tự động từ ca làm/đơn nghỉ.
        if (request.IsReset)
        {
            if (!assignment.ActualWorkedHours.HasValue && string.IsNullOrEmpty(assignment.WorkdayAdjustmentReason))
            {
                // Không có thay đổi trước đó
                return ServiceResult.Success();
            }

            assignment.ActualWorkedHours = null;
            assignment.WorkdayAdjustmentReason = null;
            assignment.UpdatedAt = _clock.UtcNow;

            var newValues = new
            {
                ActualWorkedHours = (decimal?)null,
                WorkdayAdjustmentReason = (string?)null,
                EffectiveWorkedHours = systemCalculatedHours
            };

            _auditLogRepository.Add(AuditLogFactory.Create(
                actorAccountId,
                "ADJUST_WORKDAY_HOURS",
                nameof(ShiftAssignment),
                assignment.ShiftAssignmentId.ToString(),
                prevValues,
                newValues,
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }

        if (!request.ActualWorkedHours.HasValue)
        {
            return ServiceResult.Failure("HoursRequired", "Vui lòng nhập số giờ làm việc thực tế.");
        }

        decimal actualHours = request.ActualWorkedHours.Value;
        if (actualHours < 0m || actualHours > 24m)
        {
            return ServiceResult.Failure("HoursOutOfRange",
                "Số giờ làm việc thực tế phải từ 0 đến 24 giờ.");
        }

        // Nếu giá trị nhập trùng giá trị hệ thống thì không lưu điều chỉnh thủ công dư thừa.
        if (actualHours == systemCalculatedHours)
        {
            if (!assignment.ActualWorkedHours.HasValue && string.IsNullOrEmpty(assignment.WorkdayAdjustmentReason))
            {
                // Không có thay đổi trước đó
                return ServiceResult.Success();
            }

            assignment.ActualWorkedHours = null;
            assignment.WorkdayAdjustmentReason = null;
            assignment.UpdatedAt = _clock.UtcNow;

            var newValues = new
            {
                ActualWorkedHours = (decimal?)null,
                WorkdayAdjustmentReason = (string?)null,
                EffectiveWorkedHours = systemCalculatedHours
            };

            _auditLogRepository.Add(AuditLogFactory.Create(
                actorAccountId,
                "ADJUST_WORKDAY_HOURS",
                nameof(ShiftAssignment),
                assignment.ShiftAssignmentId.ToString(),
                prevValues,
                newValues,
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }

        string trimmedReason = request.WorkdayAdjustmentReason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            return ServiceResult.Failure("ReasonRequired", "Bắt buộc phải nhập lý do khi điều chỉnh giờ công khác với giờ hệ thống tính.");
        }

        if (trimmedReason.Length > 500)
        {
            return ServiceResult.Failure("ReasonTooLong", "Lý do điều chỉnh không được vượt quá 500 ký tự.");
        }

        if (assignment.ActualWorkedHours == actualHours && assignment.WorkdayAdjustmentReason == trimmedReason)
        {
            // Dữ liệu không đổi so với hiện tại
            return ServiceResult.Success();
        }

        assignment.ActualWorkedHours = actualHours;
        assignment.WorkdayAdjustmentReason = trimmedReason;
        assignment.UpdatedAt = _clock.UtcNow;

        var updatedNewValues = new
        {
            ActualWorkedHours = assignment.ActualWorkedHours,
            WorkdayAdjustmentReason = assignment.WorkdayAdjustmentReason,
            EffectiveWorkedHours = actualHours
        };

        _auditLogRepository.Add(AuditLogFactory.Create(
            actorAccountId,
            "ADJUST_WORKDAY_HOURS",
            nameof(ShiftAssignment),
            assignment.ShiftAssignmentId.ToString(),
            prevValues,
            updatedNewValues,
            _clock.UtcNow));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }
}
