using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.ShiftTransfers.Repositories;
using ShiftScheduling.Features.ShiftTransfers.ViewModels;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.Results;

namespace ShiftScheduling.Features.ShiftTransfers.Services;

public sealed partial class ShiftTransferService : IShiftTransferService
{
    private readonly IShiftTransferRepository _repository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ShiftTransferService> _logger;

    public ShiftTransferService(
        IShiftTransferRepository repository,
        INotificationRepository notificationRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ShiftTransferService> logger)
    {
        _repository = repository;
        _notificationRepository = notificationRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ShiftTransferIndexViewModel> GetIndexAsync(
        int actorAccountId,
        bool isManager,
        ShiftTransferStatusFilter? managerStatus,
        CancellationToken cancellationToken = default)
    {
        if (isManager)
        {
            var managerRequests = await _repository.GetManagerRequestsAsync(cancellationToken);
            var filtered = FilterManagerRequests(managerRequests, managerStatus);
            return new ShiftTransferIndexViewModel
            {
                IsManager = true,
                ManagerStatus = managerStatus,
                ManagerRequests = filtered.Select(request => MapListItem(request, false, true)).ToArray()
            };
        }

        var employee = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
        if (employee is null)
        {
            return new ShiftTransferIndexViewModel();
        }

        var sent = await _repository.GetSentRequestsAsync(employee.EmployeeId, cancellationToken);
        var received = await _repository.GetReceivedRequestsAsync(employee.EmployeeId, cancellationToken);
        return new ShiftTransferIndexViewModel
        {
            SentRequests = sent.Select(request => MapListItem(request, false, false)).ToArray(),
            ReceivedRequests = received.Select(request => MapListItem(request, true, false)).ToArray()
        };
    }

    /// <inheritdoc />
    public async Task<ShiftTransferCreateViewModel?> PrepareCreateAsync(
        int actorAccountId,
        ShiftTransferCreateViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        var sender = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
        if (sender is null)
        {
            return null;
        }

        model ??= new ShiftTransferCreateViewModel();
        var assignments = await _repository.GetPublishedAssignmentsForEmployeeAsync(
            sender.EmployeeId,
            cancellationToken);
        var eligibleAssignments = assignments
            .Where(assignment => !HasShiftStarted(assignment))
            .ToArray();
        model.Assignments = eligibleAssignments.Select(MapAssignmentOption).ToArray();
        model.Receivers = await GetReceiverOptionsAsync(sender, model.ShiftAssignmentId, cancellationToken);
        return model;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SubmitAsync(
        ShiftTransferCreateViewModel model,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var sender = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
        if (sender is null)
        {
            return ServiceResult.Failure(ErrorCodes.EmployeeNotFound, "Không tìm thấy hồ sơ nhân viên của tài khoản.");
        }

        var assignment = await _repository.GetAssignmentByIdAsync(model.ShiftAssignmentId, true, cancellationToken);
        var requestValidation = await ValidateNewRequestAsync(sender, assignment, model, cancellationToken);
        if (!requestValidation.Succeeded)
        {
            return requestValidation;
        }

        var request = BuildRequest(sender.EmployeeId, model);
        _repository.Add(request);
        AddAuditLog(actorAccountId, "SUBMIT_SHIFT_TRANSFER", request, null, BuildAuditValues(request));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await TrySaveRequestNotificationAsync(request, cancellationToken);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ShiftTransferDetailsViewModel?> GetDetailsAsync(
        int shiftTransferRequestId,
        int actorAccountId,
        bool isManager,
        CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetRequestByIdAsync(shiftTransferRequestId, true, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (isManager)
        {
            return MapDetails(request, null, true);
        }

        var employee = await GetCurrentEmployeeAsync(actorAccountId, true, cancellationToken);
        if (employee is null
            || (request.SenderEmployeeId != employee.EmployeeId
                && request.ReceiverEmployeeId != employee.EmployeeId))
        {
            return null;
        }

        return MapDetails(request, employee.EmployeeId, false);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RespondAsync(
        int shiftTransferRequestId,
        bool accept,
        int actorAccountId,
        CancellationToken cancellationToken = default)
    {
        var employee = await GetCurrentEmployeeAsync(actorAccountId, false, cancellationToken);
        var request = await _repository.GetRequestByIdAsync(shiftTransferRequestId, false, cancellationToken);
        var validation = ValidateReceiverResponse(employee, request);
        if (!validation.Succeeded)
        {
            return validation;
        }

        // UC10: Người nhận chỉ phản hồi đồng ý hoặc từ chối.
        // BR16 được áp dụng ở bước Quản lý kiểm tra và ngay trước khi cập nhật lịch chính thức.
        var previousValues = BuildAuditValues(request!);
        ApplyReceiverResponse(request!, accept);
        AddAuditLog(
            actorAccountId,
            "RESPOND_SHIFT_TRANSFER",
            request!,
            previousValues,
            BuildAuditValues(request!));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAfterReceiverResponseAsync(request!, accept, cancellationToken);
        return ServiceResult.Success();
    }

    /// <inheritdoc />
    public async Task<ShiftTransferReviewViewModel?> PrepareReviewAsync(
        int shiftTransferRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetRequestByIdAsync(shiftTransferRequestId, true, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var validation = await ValidateTransferAsync(request, cancellationToken);
        return new ShiftTransferReviewViewModel
        {
            ShiftTransferRequestId = request.ShiftTransferRequestId,
            ManagerNote = request.ManagerNote,
            Details = MapDetails(request, null, true),
            BlockingErrors = validation.BlockingErrors,
            Warnings = validation.Warnings
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReviewAsync(
        int shiftTransferRequestId,
        bool approve,
        string? managerNote,
        int managerAccountId,
        CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetRequestByIdAsync(shiftTransferRequestId, false, cancellationToken);
        var stateValidation = ValidateManagerReviewState(request);
        if (!stateValidation.Succeeded)
        {
            return stateValidation;
        }

        if (approve)
        {
            var latestValidation = await ValidateTransferAsync(request!, cancellationToken);
            if (latestValidation.BlockingErrors.Count > 0)
            {
                return ServiceResult.Failure(
                    ErrorCodes.ShiftTransferRuleViolation,
                    latestValidation.BlockingErrors[0]);
            }
        }

        var previousValues = BuildAuditValues(request!);
        ApplyManagerDecision(request!, approve, managerNote, managerAccountId);
        AddAuditLog(
            managerAccountId,
            "REVIEW_SHIFT_TRANSFER",
            request!,
            previousValues,
            BuildAuditValues(request!));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyTransferResultAsync(request!, cancellationToken);
        return ServiceResult.Success();
    }
}
