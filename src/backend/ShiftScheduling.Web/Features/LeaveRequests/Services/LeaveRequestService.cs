using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Email;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.LeaveRequests.ViewModels;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.LeaveRequests.Services;

public sealed partial class LeaveRequestService : ILeaveRequestService
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<LeaveRequestService> _logger;
    private readonly IEmailSender? _emailSender;

    public LeaveRequestService(
        ILeaveRequestRepository leaveRequestRepository,
        INotificationRepository notificationRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<LeaveRequestService> logger,
        IEmailSender? emailSender = null)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _notificationRepository = notificationRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
        _emailSender = emailSender;
    }

    /// <inheritdoc />
    public async Task<LeaveRequestIndexViewModel> GetIndexAsync(
        int actorAccountId,
        bool isManager,
        int? schedulePeriodId,
        LeaveRequestStatus? status,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 5, 50);

        var periods = await _leaveRequestRepository.GetPeriodsAsync(cancellationToken);
        PagedResult<LeaveRequest> pagedRequests;

        if (isManager)
        {
            pagedRequests = await _leaveRequestRepository.GetPagedForManagerAsync(
                schedulePeriodId,
                status,
                normalizedPage,
                normalizedPageSize,
                cancellationToken);
        }
        else
        {
            var employee = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
            pagedRequests = employee is null
                ? new PagedResult<LeaveRequest>([], normalizedPage, normalizedPageSize, 0)
                : await _leaveRequestRepository.GetPagedForEmployeeAsync(
                    employee.EmployeeId,
                    schedulePeriodId,
                    status,
                    normalizedPage,
                    normalizedPageSize,
                    cancellationToken);
        }

        var listItems = await MapListItemsAsync(pagedRequests.Items, cancellationToken);

        return new LeaveRequestIndexViewModel
        {
            IsManager = isManager,
            SchedulePeriodId = schedulePeriodId,
            Status = status,
            Periods = periods.Select(MapPeriodOption).ToArray(),
            Requests = new PagedResult<LeaveRequestListItemViewModel>(
                listItems,
                pagedRequests.Page,
                pagedRequests.PageSize,
                pagedRequests.TotalItems)
        };
    }

    /// <inheritdoc />
    public async Task<LeaveRequestCreateViewModel?> PrepareCreateAsync(
        int actorAccountId,
        LeaveRequestCreateViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var assignments = await _leaveRequestRepository.GetEmployeePublishedAssignmentsAsync(
            employee.EmployeeId,
            _clock.TodayInVietnam,
            cancellationToken);

        assignments = assignments
            .Where(a => ValidateNotEnded(a.WorkDate, a.ShiftType).Succeeded)
            .ToArray();

        var eligiblePeriods = assignments
            .Select(a => a.WorkSchedule.SchedulePeriod)
            .GroupBy(p => p.SchedulePeriodId)
            .Select(g => g.First())
            .OrderByDescending(p => p.WeekStartDate)
            .ToArray();

        model ??= new LeaveRequestCreateViewModel { LeaveDate = _clock.TodayInVietnam };
        model.Periods = eligiblePeriods.Select(MapPeriodOption).ToArray();
        model.AvailableAssignments = assignments.Select(a => new LeaveAssignmentOptionViewModel(
            a.WorkSchedule.SchedulePeriodId,
            a.WorkDate,
            a.ShiftTypeId,
            a.ShiftType?.ShiftName ?? string.Empty,
            a.ShiftType?.StartTime ?? TimeOnly.MinValue,
            a.ShiftType?.EndTime ?? TimeOnly.MinValue
        )).ToArray();

        if (model.SchedulePeriodId == 0 && eligiblePeriods.Length > 0)
        {
            var today = _clock.TodayInVietnam;
            var defaultPeriod = eligiblePeriods
                .FirstOrDefault(p => p.WeekStartDate <= today && today <= p.WeekEndDate)
                ?? eligiblePeriods
                    .Where(p => p.WeekStartDate > today)
                    .OrderBy(p => p.WeekStartDate)
                    .FirstOrDefault()
                ?? eligiblePeriods
                    .Where(p => p.WeekEndDate < today)
                    .OrderByDescending(p => p.WeekEndDate)
                    .FirstOrDefault()
                ?? eligiblePeriods[0];

            model.SchedulePeriodId = defaultPeriod.SchedulePeriodId;
        }

        var availableDates = assignments
            .Where(a => a.WorkSchedule.SchedulePeriodId == model.SchedulePeriodId)
            .Select(a => a.WorkDate)
            .Distinct()
            .OrderBy(d => d)
            .ToArray();
        model.AvailableDates = availableDates;

        if (model.LeaveDate == default || !availableDates.Contains(model.LeaveDate))
        {
            if (availableDates.Length > 0)
            {
                model.LeaveDate = availableDates[0];
            }
        }

        var shiftsOnDate = assignments
            .Where(a => a.WorkSchedule.SchedulePeriodId == model.SchedulePeriodId
                && a.WorkDate == model.LeaveDate)
            .Select(a => a.ShiftType)
            .Where(s => s != null)
            .GroupBy(s => s!.ShiftTypeId)
            .Select(g => g.First()!)
            .OrderBy(s => s.DisplayOrder)
            .ToArray();
        model.ShiftTypes = shiftsOnDate.Select(MapShiftOption).ToArray();

        return model;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SubmitAsync(
        LeaveRequestCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
        if (employee is null)
        {
            return ServiceResult.Failure(
            ErrorCodes.EmployeeNotFound,
            "Không tìm thấy hồ sơ nhân viên của tài khoản.");
        }

        var validation = await ValidateSubmissionAsync(employee, model, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var request = BuildLeaveRequest(employee.EmployeeId, model);
        _leaveRequestRepository.Add(request);
        AddAuditLog(actorAccountId, "SUBMIT_LEAVE_REQUEST", request, null, BuildAuditValues(request));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TryNotifyManagersOnSubmitAsync(employee, request, cancellationToken);

        _logger.LogInformation(
            "Leave request submitted. ActorAccountId: {ActorAccountId}; LeaveRequestId: {LeaveRequestId}",
            actorAccountId,
            request.LeaveRequestId);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<LeaveRequestDetailsViewModel?> GetDetailsAsync(
        int leaveRequestId,
        int actorAccountId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        var request = await _leaveRequestRepository.GetByIdAsync(leaveRequestId, true, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (!isManager)
        {
            var employee = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
            if (employee is null || employee.EmployeeId != request.EmployeeId)
            {
                return null;
            }
        }

        return await MapDetailsAsync(request, isManager, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LeaveRequestReviewViewModel?> PrepareReviewAsync(
        int leaveRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _leaveRequestRepository.GetByIdAsync(leaveRequestId, true, cancellationToken);
        if (request is null)
        {
            return null;
        }

        return new LeaveRequestReviewViewModel
        {
            LeaveRequestId = request.LeaveRequestId,
            RowVersion = Convert.ToBase64String(request.RowVersion),
            ManagerNote = request.ManagerNote,
            Details = await MapDetailsAsync(request, true, cancellationToken)
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReviewAsync(
        int leaveRequestId,
        bool approve,
        string? managerNote,
        int managerAccountId,
        string rowVersion = "",
        CancellationToken cancellationToken = default)
    {
        var request = await _leaveRequestRepository.GetByIdAsync(leaveRequestId, false, cancellationToken);
        if (request is null)
        {
            return ServiceResult.Failure(ErrorCodes.LeaveRequestNotFound, "Không tìm thấy đơn xin nghỉ.");
        }

        // BR16: Phải đọc trạng thái mới nhất trước khi Quản lý xử lý đơn.
        if (request.Status != LeaveRequestStatus.Pending)
        {
            return ServiceResult.Failure(
                ErrorCodes.LeaveRequestAlreadyReviewed,
                "Đơn xin nghỉ đã được xử lý hoặc trạng thái đã thay đổi.");
        }

        byte[] expectedRowVersion;
        try
        {
            expectedRowVersion = string.IsNullOrWhiteSpace(rowVersion)
                ? []
                : Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            return ServiceResult.Failure(
                ErrorCodes.ConcurrencyConflict,
                "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.");
        }

        if (expectedRowVersion.Length > 0)
        {
            _leaveRequestRepository.SetOriginalRowVersion(request, expectedRowVersion);
        }

        var previousValues = BuildAuditValues(request);
        request.Status = approve ? LeaveRequestStatus.Approved : LeaveRequestStatus.Rejected;
        request.ApprovedByAccountId = managerAccountId;
        request.ReviewedAt = _clock.UtcNow;
        request.ManagerNote = NormalizeOptionalText(managerNote);

        AddAuditLog(managerAccountId, "REVIEW_LEAVE_REQUEST", request, previousValues, BuildAuditValues(request));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await TryNotifyEmployeeAsync(request, cancellationToken);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CancelAsync(
        int leaveRequestId,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
        if (employee is null)
        {
            return ServiceResult.Failure(
                ErrorCodes.EmployeeNotFound,
                "Không tìm thấy hồ sơ nhân viên của tài khoản.");
        }

        var request = await _leaveRequestRepository.GetByIdAsync(leaveRequestId, false, cancellationToken);
        if (request is null)
        {
            return ServiceResult.Failure(ErrorCodes.LeaveRequestNotFound, "Không tìm thấy đơn xin nghỉ.");
        }

        if (request.EmployeeId != employee.EmployeeId)
        {
            return ServiceResult.Failure(
                ErrorCodes.InvalidShiftAssignment,
                "Bạn không có quyền thao tác trên đơn xin nghỉ này.");
        }

        if (request.Status != LeaveRequestStatus.Pending)
        {
            return ServiceResult.Failure(
                ErrorCodes.LeaveRequestAlreadyReviewed,
                "Chỉ có thể hủy đơn xin nghỉ khi đơn đang ở trạng thái Chờ duyệt.");
        }

        var previousValues = BuildAuditValues(request);
        request.Status = LeaveRequestStatus.Cancelled;
        AddAuditLog(actorAccountId, "CANCEL_LEAVE_REQUEST", request, previousValues, BuildAuditValues(request));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Leave request cancelled. ActorAccountId: {ActorAccountId}; LeaveRequestId: {LeaveRequestId}",
            actorAccountId,
            request.LeaveRequestId);
        return ServiceResult.Success();
    }
}
