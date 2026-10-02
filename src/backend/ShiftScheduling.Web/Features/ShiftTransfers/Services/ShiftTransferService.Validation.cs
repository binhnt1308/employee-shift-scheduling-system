using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.ShiftTransfers.Services;

public sealed partial class ShiftTransferService
{
    private async Task<ServiceResult> ValidateNewRequestAsync(
        Employee sender,
        ShiftAssignment? assignment,
        ShiftTransferCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (assignment is null || assignment.EmployeeId != sender.EmployeeId)
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftAssignmentNotFound,
                "Ca cần chuyển không thuộc lịch của bạn.");
        }

        var assignmentState = ValidateAssignmentState(assignment, sender.EmployeeId);
        if (!assignmentState.Succeeded)
        {
            return assignmentState;
        }

        if (await _repository.HasActiveRequestForAssignmentAsync(assignment.ShiftAssignmentId, cancellationToken))
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftTransferDuplicate,
                "Ca này đã có yêu cầu đổi ca đang chờ xử lý.");
        }

        var receiver = await GetEmployeeByIdFromCandidatesAsync(
            sender,
            model.ReceiverEmployeeId,
            cancellationToken);
        if (receiver is null)
        {
            return ServiceResult.Failure(
                ErrorCodes.ShiftTransferInvalidReceiver,
                "Người nhận không còn hoạt động, không cùng vị trí"
                    + " hoặc không có tài khoản hợp lệ.");
        }

        var validation = await ValidateTransferAsync(assignment, sender, receiver, cancellationToken);
        return validation.BlockingErrors.Count == 0
            ? ServiceResult.Success()
            : ServiceResult.Failure(ErrorCodes.ShiftTransferRuleViolation, validation.BlockingErrors[0]);
    }

    private ServiceResult ValidateAssignmentState(ShiftAssignment assignment, int senderEmployeeId)
    {
        // BR17: Chỉ chuyển ca một chiều từ đúng người đang được phân trong lịch chính thức.
        if (assignment.EmployeeId != senderEmployeeId
            || assignment.WorkSchedule.Status != ScheduleStatus.Published)
        {
            return ServiceResult.Failure(
                ErrorCodes.PublishedScheduleRequired,
                "Chỉ được chuyển ca đang thuộc lịch chính thức của bạn.");
        }

        return HasShiftStarted(assignment)
            ? ServiceResult.Failure(
                ErrorCodes.ShiftAlreadyStarted,
                "Ca làm việc đã bắt đầu hoặc đã kết thúc.")
            : ServiceResult.Success();
    }

    private async Task<TransferValidationResult> ValidateTransferAsync(
        ShiftTransferRequest request, CancellationToken cancellationToken)
    {
        return await ValidateTransferAsync(
            request.ShiftAssignment, request.SenderEmployee, request.ReceiverEmployee, cancellationToken);
    }

    private async Task<TransferValidationResult> ValidateTransferAsync(
        ShiftAssignment assignment, Employee sender, Employee receiver, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Kiểm tra ca, người gửi và người nhận trước để không truy vấn thêm dữ liệu
        // khi điều kiện nền tảng đã không hợp lệ.
        AddAssignmentAndIdentityErrors(assignment, sender, receiver, errors);
        if (errors.Count > 0)
        {
            return new TransferValidationResult(errors, warnings);
        }

        await AddAvailabilityAndLeaveErrorsAsync(assignment, receiver, errors, cancellationToken);
        var senderAssignments = await _repository.GetPublishedAssignmentsForEmployeeInPeriodAsync(
            sender.EmployeeId, assignment.WorkSchedule.SchedulePeriodId, cancellationToken);
        var receiverAssignments = await _repository.GetPublishedAssignmentsForEmployeeInPeriodAsync(
            receiver.EmployeeId, assignment.WorkSchedule.SchedulePeriodId, cancellationToken);

        // BR08-BR11 trong UC10 được kiểm tra trên người nhận sau khi nhận thêm ca.
        AddReceiverScheduleRuleErrors(assignment, receiver, receiverAssignments, errors);
        await AddStaffingErrorsAsync(assignment, receiver, errors, cancellationToken);
        AddWorkloadWarnings(assignment, sender, receiver, senderAssignments, receiverAssignments, warnings);
        return new TransferValidationResult(errors, warnings);
    }

    private TransferValidationResult ValidateTransferInMemory(
        ShiftAssignment assignment, Employee sender, Employee receiver,
        IReadOnlyList<ShiftAssignment> senderAssignments, IReadOnlyList<ShiftAssignment> receiverAssignments,
        bool isReceiverAvailable, bool hasReceiverApprovedLeave,
        StaffingRequirement? requirement, IReadOnlyList<ShiftAssignment> shiftAssignments)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        AddAssignmentAndIdentityErrors(assignment, sender, receiver, errors);
        if (errors.Count > 0)
        {
            return new TransferValidationResult(errors, warnings);
        }

        AddAvailabilityAndLeaveErrors(isReceiverAvailable, hasReceiverApprovedLeave, errors);
        AddReceiverScheduleRuleErrors(assignment, receiver, receiverAssignments, errors);
        AddStaffingErrors(assignment, receiver, requirement, shiftAssignments, errors);
        AddWorkloadWarnings(assignment, sender, receiver, senderAssignments, receiverAssignments, warnings);

        return new TransferValidationResult(errors, warnings);
    }

    private static void AddAvailabilityAndLeaveErrors(
        bool isReceiverAvailable, bool hasReceiverApprovedLeave, List<string> errors)
    {
        if (!isReceiverAvailable)
        {
            errors.Add("Người nhận chưa đăng ký rảnh tại ca này.");
        }

        if (hasReceiverApprovedLeave)
        {
            errors.Add("Người nhận có đơn xin nghỉ đã được duyệt tại ca này.");
        }
    }

    private static void AddStaffingErrors(
        ShiftAssignment target, Employee receiver, StaffingRequirement? requirement,
        IReadOnlyList<ShiftAssignment> assignments, List<string> errors)
    {
        if (requirement is null)
        {
            errors.Add("Không tìm thấy định mức mới nhất của ca để kiểm tra.");
            return;
        }

        // Loại người gửi khỏi ca rồi giả lập thêm người nhận để kiểm tra định mức theo vị trí sau khi chuyển.
        var employees = assignments
            .Where(item => item.ShiftAssignmentId != target.ShiftAssignmentId)
            .Select(item => item.Employee)
            .Append(receiver)
            .ToArray();
        var sales = employees.Count(employee => employee.Position == EmployeePosition.Sales);
        var cashiers = employees.Count(employee => employee.Position == EmployeePosition.Cashier);
        if (sales < requirement.RequiredSalesStaff || cashiers < requirement.RequiredCashiers)
        {
            errors.Add("Sau khi đổi ca, ca không còn đáp ứng định mức nhân sự theo vị trí.");
        }
    }

    private void AddAssignmentAndIdentityErrors(
        ShiftAssignment assignment,
        Employee sender,
        Employee receiver,
        List<string> errors)
    {
        if (assignment.WorkSchedule.Status != ScheduleStatus.Published
            || assignment.EmployeeId != sender.EmployeeId)
        {
            errors.Add("Ca không còn thuộc lịch chính thức của người gửi.");
        }

        if (HasShiftStarted(assignment))
        {
            errors.Add("Ca làm việc đã bắt đầu hoặc đã kết thúc.");
        }

        if (!receiver.IsActive || receiver.UserAccountId is null || receiver.UserAccount?.IsLocked == true)
        {
            errors.Add("Người nhận không còn là nhân viên hoạt động có tài khoản hợp lệ.");
        }

        // BR07: Chuyển ca chỉ giữ nguyên đúng vị trí Tư vấn hoặc Thu ngân của ca.
        if (receiver.Position != sender.Position)
        {
            errors.Add("Người nhận không cùng vị trí công việc với người gửi.");
        }
    }

    private async Task AddAvailabilityAndLeaveErrorsAsync(
        ShiftAssignment assignment,
        Employee receiver,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var periodId = assignment.WorkSchedule.SchedulePeriodId;
        // BR05: Người nhận phải đăng ký rảnh đúng ngày và ca cần nhận.
        if (!await _repository.HasAvailabilityAsync(
                receiver.EmployeeId,
                periodId,
                assignment.WorkDate,
                assignment.ShiftTypeId,
                cancellationToken))
        {
            errors.Add("Người nhận chưa đăng ký rảnh tại ca này.");
        }

        // BR06: Người nhận không được có đơn xin nghỉ đã duyệt tại ngày hoặc ca tương ứng.
        if (await _repository.HasApprovedLeaveAsync(
                receiver.EmployeeId,
                periodId,
                assignment.WorkDate,
                assignment.ShiftTypeId,
                cancellationToken))
        {
            errors.Add("Người nhận có đơn xin nghỉ đã được duyệt tại ca này.");
        }
    }

    private void AddReceiverScheduleRuleErrors(
        ShiftAssignment target,
        Employee receiver,
        IReadOnlyList<ShiftAssignment> existingAssignments,
        List<string> errors)
    {
        var period = target.WorkSchedule?.SchedulePeriod;
        if (period is null)
        {
            errors.Add("Không tìm thấy kỳ lịch tương ứng với ca làm việc.");
            return;
        }

        var availableShifts = existingAssignments
            .Select(a => a.ShiftType)
            .Append(target.ShiftType)
            .Where(st => st != null)
            .Cast<ShiftType>()
            .ToList();

        var periodShifts = DailyShiftRuleEvaluator.ExtractPeriodShifts(period.StaffingRequirements, availableShifts);
        if (!DailyShiftRuleEvaluator.TryResolvePolicy(periodShifts, out var policy, out var policyError))
        {
            errors.Add(policyError ?? "Không xác định được chính sách ca làm việc hợp lệ của kỳ lịch.");
            return;
        }

        var (policyMax, allowSplit) = policy;
        var projectedAssignments = existingAssignments.Append(target).ToArray();

        // Kiểm tra quy tắc ca trong ngày (BR-01, BR08, BR09, BR10) qua DailyShiftRuleEvaluator
        foreach (var group in projectedAssignments.GroupBy(item => item.WorkDate))
        {
            var dayShifts = group
                .Select(item => item.ShiftType)
                .Where(st => st != null)
                .ToList();

            DailyShiftRuleEvaluator.ValidateDailyAssignments(
                receiver, group.Key, dayShifts!, policyMax, allowSplit, errors);
        }

        // BR11: FT không được vượt quá 6 ngày làm trong một kỳ lịch tuần.
        if (receiver.EmploymentType == EmploymentType.FullTime
            && projectedAssignments.Select(item => item.WorkDate).Distinct().Count() > 6)
        {
            errors.Add("Việc đổi ca làm nhân viên toàn thời gian vượt quá"
                + " 6 ngày làm việc trong tuần.");
        }

        var shiftTypeMap = projectedAssignments
            .Where(a => a.ShiftType is not null)
            .GroupBy(a => a.ShiftTypeId)
            .ToDictionary(g => g.Key, g => g.First().ShiftType);
        var weekStartDate = period.WeekStartDate;
        var weekEndDate = period.WeekEndDate;

        // BR-05: Tổng giờ làm trong tuần không được vượt trần theo loại hợp đồng.
        var hoursErrors = WeeklyWorkHoursRuleEvaluator.EvaluateWeeklyWorkHours(
            receiver, projectedAssignments, shiftTypeMap, weekStartDate, weekEndDate);
        errors.AddRange(hoursErrors);

        // BR20, BR21: Kiểm tra quy tắc nghỉ tối thiểu 12h giữa ca cuối ngày D và ca đầu ngày D+1,
        // và 24h liên tục trong kỳ lịch.
        var restErrors = SchedulingRestRuleEvaluator.EvaluateEmployeeRestRules(
            receiver, projectedAssignments, shiftTypeMap, weekStartDate);
        errors.AddRange(restErrors);
    }

    private async Task AddStaffingErrorsAsync(
        ShiftAssignment target,
        Employee receiver,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var periodId = target.WorkSchedule.SchedulePeriodId;
        var requirement = await _repository.GetStaffingRequirementAsync(
            periodId,
            target.WorkDate,
            target.ShiftTypeId,
            cancellationToken);
        var assignments = await _repository.GetAssignmentsForShiftAsync(
            target.WorkScheduleId,
            target.WorkDate,
            target.ShiftTypeId,
            cancellationToken);
        if (requirement is null)
        {
            errors.Add("Không tìm thấy định mức mới nhất của ca để kiểm tra.");
            return;
        }

        var employees = assignments
            .Where(item => item.ShiftAssignmentId != target.ShiftAssignmentId)
            .Select(item => item.Employee)
            .Append(receiver)
            .ToArray();
        var sales = employees.Count(employee => employee.Position == EmployeePosition.Sales);
        var cashiers = employees.Count(employee => employee.Position == EmployeePosition.Cashier);
        if (sales < requirement.RequiredSalesStaff || cashiers < requirement.RequiredCashiers)
        {
            errors.Add("Sau khi đổi ca, ca không còn đáp ứng định mức nhân sự theo vị trí.");
        }

        // BR13: Sau khi thay người, ca vẫn phải có ít nhất một nhân viên từ Level 2 trở lên.
        if (employees.Count(employee => employee.Level >= EmployeeLevel.Level2) < 1)
        {
            errors.Add("Sau khi đổi ca, ca không còn nhân viên từ Level 2 trở lên.");
        }
    }

    private static void AddWorkloadWarnings(
        ShiftAssignment target,
        Employee sender,
        Employee receiver,
        IReadOnlyList<ShiftAssignment> senderAssignments,
        IReadOnlyList<ShiftAssignment> receiverAssignments,
        List<string> warnings)
    {
        var senderRemaining = senderAssignments
            .Where(item => item.ShiftAssignmentId != target.ShiftAssignmentId)
            .ToArray();
        var receiverProjected = receiverAssignments.Append(target).ToArray();
        AddExpectedWorkloadWarning(sender, senderRemaining, "Người gửi", warnings);
        AddExpectedWorkloadWarning(receiver, receiverProjected, "Người nhận", warnings);
    }

    private static void AddExpectedWorkloadWarning(
        Employee employee,
        IReadOnlyList<ShiftAssignment> assignments,
        string label,
        List<string> warnings)
    {
        // BR18: Mức khoảng 5 ca PT hoặc 6 ngày FT chỉ là ưu tiên, không phải lỗi bắt buộc.
        if (employee.EmploymentType == EmploymentType.PartTime)
        {
            if (assignments.Count != 5)
            {
                warnings.Add($"{label} dự kiến có {assignments.Count} ca trong tuần,"
                    + " khác mức ưu tiên khoảng 5 ca.");
            }
            return;
        }

        var days = assignments.Select(item => item.WorkDate).Distinct().Count();
        if (days != 6)
        {
            warnings.Add($"{label} dự kiến làm {days} ngày trong tuần, khác mức ưu tiên 6 ngày.");
        }
    }

    private static bool IsConsecutivePair(IReadOnlyList<string> codes)
    {
        var values = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return values.SetEquals([ShiftTypeConstants.MorningCode, ShiftTypeConstants.AfternoonCode])
            || values.SetEquals([ShiftTypeConstants.AfternoonCode, ShiftTypeConstants.EveningCode]);
    }

    private static bool Overlaps(ShiftType left, ShiftType right)
    {
        return left.StartTime < right.EndTime && right.StartTime < left.EndTime;
    }

    private sealed record TransferValidationResult(
        IReadOnlyList<string> BlockingErrors,
        IReadOnlyList<string> Warnings);
}
