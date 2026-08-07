using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.ShiftTransfers;

public sealed class ShiftTransferWorkflowTests
{
    [Fact(DisplayName = "TC10-01 - UC10 BR17 - Gửi yêu cầu hợp lệ -> chờ người nhận")]
    public async Task SubmitAsync_WhenValid_CreatesPendingReceiverRequest()
    {
        var context = ShiftTransferTestContext.Create();

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.True(result.Succeeded);
        var request = Assert.Single(context.Repository.Requests);
        Assert.Equal(ReceiverStatus.Pending, request.ReceiverStatus);
        Assert.Equal(ApprovalStatus.Pending, request.ApprovalStatus);
        Assert.Equal(context.Sender.EmployeeId, request.SenderEmployeeId);
        Assert.Equal(context.Receiver.EmployeeId, request.ReceiverEmployeeId);
        Assert.Contains(context.Notifications.Items, item =>
            item.ReceiverAccountId == context.Receiver.UserAccountId
            && item.Type == NotificationType.ShiftTransferRequest);
    }

    [Fact(DisplayName = "TC10-02 - UC10 - Ca đã có yêu cầu đang hiệu lực -> từ chối tạo trùng")]
    public async Task SubmitAsync_WhenActiveRequestExists_ReturnsDuplicateError()
    {
        var context = ShiftTransferTestContext.Create();
        context.CreateRequest();

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferDuplicate, result.ErrorCode);
        Assert.Single(context.Repository.Requests);
    }

    [Fact(DisplayName = "TC10-03 - UC10 BR17 - Người gửi không sở hữu ca -> từ chối")]
    public async Task SubmitAsync_WhenSenderDoesNotOwnAssignment_ReturnsAssignmentNotFound()
    {
        var context = ShiftTransferTestContext.Create();

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 20);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftAssignmentNotFound, result.ErrorCode);
        Assert.Empty(context.Repository.Requests);
    }

    [Fact(DisplayName = "TC10-04 - UC10 - Người nhận đồng ý -> chờ Quản lý duyệt")]
    public async Task RespondAsync_WhenReceiverAccepts_MovesToManagerReview()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest();

        var result = await context.Service.RespondAsync(request.ShiftTransferRequestId, true, 20);

        Assert.True(result.Succeeded);
        Assert.Equal(ReceiverStatus.Accepted, request.ReceiverStatus);
        Assert.Equal(ApprovalStatus.Pending, request.ApprovalStatus);
        Assert.NotNull(request.ReceiverRespondedAt);
        Assert.Contains(context.Notifications.Items, item => item.ReceiverAccountId == 2);
    }

    [Fact(DisplayName = "TC10-05 - UC10 - Người nhận từ chối -> lịch giữ nguyên")]
    public async Task RespondAsync_WhenReceiverRejects_RejectsRequestAndKeepsAssignment()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest();
        var originalEmployeeId = context.TargetAssignment.EmployeeId;

        var result = await context.Service.RespondAsync(request.ShiftTransferRequestId, false, 20);

        Assert.True(result.Succeeded);
        Assert.Equal(ReceiverStatus.Rejected, request.ReceiverStatus);
        Assert.Equal(ApprovalStatus.Rejected, request.ApprovalStatus);
        Assert.Equal(originalEmployeeId, context.TargetAssignment.EmployeeId);
    }

    [Fact(DisplayName = "TC10-06 - BR01 - Nhân viên khác không được phản hồi yêu cầu")]
    public async Task RespondAsync_WhenActorIsNotReceiver_ReturnsNotFound()
    {
        var context = ShiftTransferTestContext.Create();
        var thirdEmployee = ShiftTransferTestContext.CreateEmployee(
            3,
            30,
            "NV03",
            "Lê Chi",
            EmployeeLevel.Level2);
        context.Repository.Employees.Add(thirdEmployee);
        var request = context.CreateRequest();

        var result = await context.Service.RespondAsync(request.ShiftTransferRequestId, true, 30);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferNotFound, result.ErrorCode);
        Assert.Equal(ReceiverStatus.Pending, request.ReceiverStatus);
    }

    [Fact(DisplayName = "TC10-07 - UC10 - Quản lý không được duyệt trước khi người nhận đồng ý")]
    public async Task ReviewAsync_WhenReceiverHasNotAccepted_ReturnsNotAccepted()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest();

        var result = await context.Service.ReviewAsync(request.ShiftTransferRequestId, true, null, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferNotAccepted, result.ErrorCode);
        Assert.Equal(context.Sender.EmployeeId, context.TargetAssignment.EmployeeId);
    }

    [Fact(DisplayName = "TC10-08 - UC10 BR16 BR17 - Duyệt hợp lệ -> chuyển assignment một chiều")]
    public async Task ReviewAsync_WhenAcceptedAndValid_TransfersAssignmentAtomically()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);

        var result = await context.Service.ReviewAsync(
            request.ShiftTransferRequestId,
            true,
            "Đồng ý chuyển ca",
            2);

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalStatus.Approved, request.ApprovalStatus);
        Assert.Equal(context.Receiver.EmployeeId, context.TargetAssignment.EmployeeId);
        Assert.Same(context.Receiver, context.TargetAssignment.Employee);
        Assert.Equal(AssignmentSource.ShiftTransfer, context.TargetAssignment.Source);
        Assert.Equal("Đồng ý chuyển ca", request.ManagerNote);
        Assert.Equal(2, context.Notifications.Items.Count);
    }

    [Fact(DisplayName = "TC10-09 - UC10 - Quản lý từ chối -> lịch chính thức không đổi")]
    public async Task ReviewAsync_WhenManagerRejects_KeepsPublishedAssignment()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);

        var result = await context.Service.ReviewAsync(request.ShiftTransferRequestId, false, "Không phù hợp", 2);

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovalStatus.Rejected, request.ApprovalStatus);
        Assert.Equal(context.Sender.EmployeeId, context.TargetAssignment.EmployeeId);
        Assert.Equal(AssignmentSource.Manual, context.TargetAssignment.Source);
    }

    [Fact(DisplayName = "TC10-10 - UC10 - Quản lý xem và lọc yêu cầu theo trạng thái")]
    public async Task GetIndexAsync_WhenManagerFilters_ReturnsMatchingRequests()
    {
        var context = ShiftTransferTestContext.Create();
        context.CreateRequest(id: 1);
        context.CreateRequest(id: 2, receiverStatus: ReceiverStatus.Accepted);
        context.CreateRequest(
            id: 3,
            receiverStatus: ReceiverStatus.Accepted,
            approvalStatus: ApprovalStatus.Approved);
        context.CreateRequest(
            id: 4,
            receiverStatus: ReceiverStatus.Rejected,
            approvalStatus: ApprovalStatus.Rejected);

        var all = await context.Service.GetIndexAsync(2, true, null);
        var rejected = await context.Service.GetIndexAsync(
            2,
            true,
            ShiftScheduling.Features.ShiftTransfers.ViewModels.ShiftTransferStatusFilter.Rejected);

        Assert.Equal(4, all.ManagerRequests.Count);
        var item = Assert.Single(rejected.ManagerRequests);
        Assert.Equal(4, item.ShiftTransferRequestId);
    }
}
