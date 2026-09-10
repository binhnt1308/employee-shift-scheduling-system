using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.ShiftTransfers;

public sealed class ShiftTransferRuleTests
{
    [Fact(DisplayName = "TC10-R01 - UC10 BR05 - Người nhận chưa đăng ký rảnh -> chặn")]
    public async Task SubmitAsync_WhenReceiverUnavailable_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.Repository.Availabilities.Clear();

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("chưa đăng ký rảnh", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R02 - UC10 BR06 - Người nhận có đơn nghỉ đã duyệt -> chặn")]
    public async Task SubmitAsync_WhenReceiverHasApprovedLeave_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.AddApprovedLeaveForReceiver();

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("đơn xin nghỉ", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R03 - UC10 BR07 - Người nhận khác vị trí -> chặn")]
    public async Task SubmitAsync_WhenReceiverHasDifferentPosition_ReturnsInvalidReceiver()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.Position = EmployeePosition.Cashier;

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferInvalidReceiver, result.ErrorCode);
    }

    [Fact(DisplayName = "TC10-R04 - UC10 BR08 - Người nhận có ca trùng thời gian -> chặn")]
    public async Task SubmitAsync_WhenReceiverHasOverlappingShift_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.AddReceiverAssignment(context.TargetAssignment.WorkDate, context.Morning);

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("trùng thời gian", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R05 - UC10 BR09 - PT nhận ca thứ hai trong ngày -> chặn")]
    public async Task SubmitAsync_WhenPartTimeWouldHaveSecondShiftInDay_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.TargetAssignment.ShiftTypeId = context.Afternoon.ShiftTypeId;
        context.TargetAssignment.ShiftType = context.Afternoon;
        context.Repository.Availabilities.Clear();
        context.Repository.Availabilities.Add(new Availability
        {
            EmployeeId = context.Receiver.EmployeeId,
            SchedulePeriodId = context.Period.SchedulePeriodId,
            WorkDate = context.TargetAssignment.WorkDate,
            ShiftTypeId = context.Afternoon.ShiftTypeId
        });
        context.Repository.Requirements.Clear();
        context.Repository.Requirements.Add(new StaffingRequirement
        {
            SchedulePeriodId = context.Period.SchedulePeriodId,
            WorkDate = context.TargetAssignment.WorkDate,
            ShiftTypeId = context.Afternoon.ShiftTypeId,
            RequiredSalesStaff = 1,
            RequiredExperiencedStaff = 1,
            MinimumLevel = EmployeeLevel.Level2
        });
        context.AddReceiverAssignment(context.TargetAssignment.WorkDate, context.Morning);

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("bán thời gian", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R06 - UC10 BR10 - FT nhận tổ hợp sáng-tối -> chặn")]
    public async Task SubmitAsync_WhenFullTimeWouldHaveMorningEveningPair_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.EmploymentType = EmploymentType.FullTime;
        context.AddReceiverAssignment(context.TargetAssignment.WorkDate, context.Evening);

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("hai ca liên tiếp", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R07 - UC10 BR13 - Sau thay người không còn Level 2+ -> chặn")]
    public async Task SubmitAsync_WhenReplacementWouldRemoveLastExperiencedEmployee_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.Level = EmployeeLevel.Level1;

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("Level 2", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R08 - UC10 BR16 - Dữ liệu đổi trước khi duyệt -> không cập nhật lịch")]
    public async Task ReviewAsync_WhenAvailabilityRemovedAfterAcceptance_BlocksApproval()
    {
        var context = ShiftTransferTestContext.Create();
        var request = context.CreateRequest(receiverStatus: ReceiverStatus.Accepted);
        context.Repository.Availabilities.Clear();

        var result = await context.Service.ReviewAsync(request.ShiftTransferRequestId, true, null, 2);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Equal(context.Sender.EmployeeId, context.TargetAssignment.EmployeeId);
        Assert.Equal(ApprovalStatus.Pending, request.ApprovalStatus);
    }

    [Fact(DisplayName = "TC10-R09 - UC10 BR11 - FT nhận ca làm thành ngày thứ 7 -> chặn")]
    public async Task SubmitAsync_WhenFullTimeWouldWorkSeventhDay_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.EmploymentType = EmploymentType.FullTime;
        var id = 2;
        context.AddReceiverAssignment(context.TargetAssignment.WorkDate, context.Afternoon, id++);
        for (var day = 1; day <= 6; day++)
        {
            var date = context.TargetAssignment.WorkDate.AddDays(day);
            context.AddReceiverAssignment(date, context.Morning, id++);
            context.AddReceiverAssignment(date, context.Afternoon, id++);
        }

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("6 ngày", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R10 - UC10 BR12 - Ca thiếu định mức sau thay người -> chặn")]
    public async Task SubmitAsync_WhenShiftWouldRemainUnderstaffed_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.Repository.Requirements.Single().RequiredSalesStaff = 2;

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("định mức", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC10-R11 - UC10 BR21 - Người nhận ca vi phạm 24h nghỉ liên tục trong kỳ" +
        " -> chặn")]
    public async Task SubmitAsync_WhenReceiverWouldViolateWeekly24hRest_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.EmploymentType = EmploymentType.PartTime;

        // Phân ca cho người nhận tất cả các ngày còn lại
        // từ Thứ 3 đến Chủ Nhật (ca Sáng 08:00-12:00)
        var id = 10;
        for (var day = 1; day <= 6; day++)
        {
            var date = context.Period.WeekStartDate.AddDays(day);
            context.AddReceiverAssignment(date, context.Morning, id++);
            context.Repository.Availabilities.Add(new Availability
            {
                EmployeeId = context.Receiver.EmployeeId,
                SchedulePeriodId = context.Period.SchedulePeriodId,
                WorkDate = date,
                ShiftTypeId = context.Morning.ShiftTypeId
            });
        }

        // Nhận ca Thứ 2 (08:00-12:00) -> người nhận sẽ có ca cả 7 ngày
        // -> tất cả gap < 24h -> vi phạm BR21
        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("BR21", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC-BR01-TRF-01 - Đổi ca tuân thủ Max=1: FT nhận ca thứ 2 trong ngày bị chặn")]
    public async Task SubmitAsync_WhenPolicyMax1_FullTimeReceiverSecondShift_ReturnsRuleViolation()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.EmploymentType = EmploymentType.FullTime;
        context.Morning.MaxShiftsPerEmployeePerDay = 1;
        context.Afternoon.MaxShiftsPerEmployeePerDay = 1;
        context.Evening.MaxShiftsPerEmployeePerDay = 1;

        // Target assignment is Morning
        // Receiver already has Afternoon on that day
        context.AddReceiverAssignment(context.TargetAssignment.WorkDate, context.Afternoon);

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTransferRuleViolation, result.ErrorCode);
        Assert.Contains("tối đa 1 ca/ngày", result.ErrorMessage ?? string.Empty);
    }

    [Fact(DisplayName = "TC-BR01-TRF-02 - Đổi ca khi AllowSplitShift=true: FT nhận cặp Sáng-Tối thành công")]
    public async Task SubmitAsync_WhenPolicyAllowsSplitShift_FullTimeMorningAndEvening_Succeeds()
    {
        var context = ShiftTransferTestContext.Create();
        context.Receiver.EmploymentType = EmploymentType.FullTime;
        context.Morning.MaxShiftsPerEmployeePerDay = 2;
        context.Morning.AllowSplitShift = true;
        context.Afternoon.MaxShiftsPerEmployeePerDay = 2;
        context.Afternoon.AllowSplitShift = true;
        context.Evening.MaxShiftsPerEmployeePerDay = 2;
        context.Evening.AllowSplitShift = true;

        // Target assignment is Morning
        // Receiver already has Evening on that day
        context.AddReceiverAssignment(context.TargetAssignment.WorkDate, context.Evening);

        var result = await context.Service.SubmitAsync(context.CreateValidModel(), 10);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }
}
