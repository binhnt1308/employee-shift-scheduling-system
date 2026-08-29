using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.Notifications.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Notifications;

public sealed class RegistrationOpenedNotificationTests
{
    [Fact(DisplayName = "TC-NC10-REG-001: Mo dang ky thanh cong gui thong bao cho danh sach account ID do repository tra ve")]
    public async Task OpenRegistration_SendsNotificationsToAccountIdsReturnedByRepository()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.AddRange(new[] { 101, 102, 103 });
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) };

        var period = CreateValidPeriodWith21Requirements();
        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(3, notificationRepo.Items.Count);
        Assert.Contains(notificationRepo.Items, n => n.ReceiverAccountId == 101);
        Assert.Contains(notificationRepo.Items, n => n.ReceiverAccountId == 102);
        Assert.Contains(notificationRepo.Items, n => n.ReceiverAccountId == 103);
    }

    [Fact(DisplayName = "TC-NC10-REG-004: Thong bao dung Type, RelatedObjectId, IsRead=false va CreatedAt")]
    public async Task OpenRegistration_NotificationHasCorrectTypeAndAttributes()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.Add(101);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var expectedTime = new DateTime(2026, 8, 5, 10, 30, 0, DateTimeKind.Utc);
        var clock = new FakeClock { UtcNow = expectedTime };

        var period = CreateValidPeriodWith21Requirements();
        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationRepo.Items);
        Assert.Equal(NotificationType.RegistrationOpened, notification.Type);
        Assert.Equal(period.SchedulePeriodId, notification.RelatedObjectId);
        Assert.False(notification.IsRead);
        Assert.Equal(expectedTime, notification.CreatedAt);
    }

    [Fact(DisplayName = "TC-NC10-REG-005: Noi dung thong bao chua dung ten ky, khoang ngay va han deadline")]
    public async Task OpenRegistration_NotificationContentIncludesPeriodNameAndDeadline()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.Add(101);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) };

        var period = CreateValidPeriodWith21Requirements();
        period.PeriodName = "Kỳ lịch tuần 33";
        period.AvailabilityDeadline = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc);
        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(notificationRepo.Items);
        Assert.Contains("Mở đăng ký lịch rảnh: Kỳ lịch tuần 33", notification.Title);
        Assert.Contains("Kỳ lịch tuần 33", notification.Content);
        Assert.Contains("10/08/2026", notification.Content);
        Assert.Contains("16/08/2026", notification.Content);
        Assert.Contains("08/08/2026", notification.Content);
    }

    [Fact(DisplayName = "TC-NC10-REG-006: Mapper dieu huong Availability/Register voi dung ID")]
    public void NotificationItemViewModel_MapsRegistrationOpenedToAvailabilityRegisterRoute()
    {
        var item = new NotificationItemViewModel
        {
            NotificationId = 1,
            Title = "Mở đăng ký lịch rảnh",
            Content = "Nội dung",
            Type = NotificationType.RegistrationOpened,
            RelatedObjectId = 42,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtVietnam = DateTime.UtcNow.AddHours(7)
        };

        Assert.Equal("Availability", item.TargetController);
        Assert.Equal("Register", item.TargetAction);
        Assert.Equal(42, item.TargetId);
    }

    [Fact(DisplayName = "TC-NC10-REG-007: Ky khong ton tai khong them thong bao va khong Save")]
    public async Task OpenRegistration_WhenPeriodNotFound_DoesNotSendNotificationOrSave()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.Add(101);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) };

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(999, actorAccountId: 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.SchedulePeriodNotFound, result.ErrorCode);
        Assert.Empty(notificationRepo.Items);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-NC10-REG-008: Sai trang thai Preparing khong them thong bao va khong Save")]
    public async Task OpenRegistration_WhenInvalidStatus_DoesNotSendNotificationOrSave()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.Add(101);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) };

        var period = CreateValidPeriodWith21Requirements();
        period.RegistrationStatus = RegistrationStatus.Open;
        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePeriodStatus, result.ErrorCode);
        Assert.Empty(notificationRepo.Items);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-NC10-REG-009: Het han dang ky khong them thong bao va khong Save")]
    public async Task OpenRegistration_WhenDeadlineExpired_DoesNotSendNotificationOrSave()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.Add(101);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc) };

        var period = CreateValidPeriodWith21Requirements();
        // Deadline 08/08 while UtcNow is 09/08
        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.RegistrationDeadlineExpired, result.ErrorCode);
        Assert.Empty(notificationRepo.Items);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(auditRepo.Items);
    }

    [Fact(DisplayName = "TC-NC10-REG-010: Goi lai sau khi Open khong tao trung thong bao")]
    public async Task OpenRegistration_CalledAgainAfterOpen_DoesNotDuplicateNotifications()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.Add(101);
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) };

        var period = CreateValidPeriodWith21Requirements();
        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result1 = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);
        Assert.True(result1.Succeeded);
        Assert.Single(notificationRepo.Items);
        Assert.Equal(1, uow.SaveCount);

        var result2 = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);
        Assert.False(result2.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePeriodStatus, result2.ErrorCode);
        Assert.Single(notificationRepo.Items);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact(DisplayName = "TC-NC10-REG-011: Trang thai, AuditLog va Notification chi goi mot SaveChanges")]
    public async Task OpenRegistration_PerformsStatusChangeAuditLogAndNotificationsInSingleSaveChanges()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepository();
        var notificationRepo = new FakeNotificationRepository();
        notificationRepo.EmployeeAccountIds.AddRange(new[] { 101, 102 });
        var uow = new FakeUnitOfWork();
        var auditRepo = new FakeAuditLogRepository();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc) };

        var period = CreateValidPeriodWith21Requirements();
        periodRepo.SchedulePeriods.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, notificationRepo, auditRepo, uow, clock, NullLogger<SchedulePeriodService>.Instance);

        var result = await service.OpenRegistrationAsync(period.SchedulePeriodId, actorAccountId: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, uow.SaveCount);
        Assert.Equal(RegistrationStatus.Open, period.RegistrationStatus);
        Assert.Single(auditRepo.Items);
        Assert.Equal(2, notificationRepo.Items.Count);
    }


    private static FakeShiftTypeRepository CreateShiftTypeRepository()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new(8, 0), EndTime = new(12, 0), DisplayOrder = 1, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new(12, 0), EndTime = new(16, 0), DisplayOrder = 2, IsActive = true });
        repo.ShiftTypes.Add(new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new(16, 0), EndTime = new(20, 0), DisplayOrder = 3, IsActive = true });
        return repo;
    }

    private static SchedulePeriod CreateValidPeriodWith21Requirements()
    {
        var start = new DateOnly(2026, 8, 10);
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ lịch 10/08 - 16/08",
            WeekStartDate = start,
            WeekEndDate = start.AddDays(6),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Preparing,
            CreatedByAccountId = 1,
            StaffingRequirements = new List<StaffingRequirement>()
        };

        var shiftTypes = new[]
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", DisplayOrder = 1, IsActive = true },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", DisplayOrder = 2, IsActive = true },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", DisplayOrder = 3, IsActive = true }
        };

        int idCounter = 1;
        for (int day = 0; day < 7; day++)
        {
            var date = start.AddDays(day);
            foreach (var shift in shiftTypes)
            {
                period.StaffingRequirements.Add(new StaffingRequirement
                {
                    StaffingRequirementId = idCounter++,
                    SchedulePeriodId = 1,
                    SchedulePeriod = period,
                    WorkDate = date,
                    ShiftTypeId = shift.ShiftTypeId,
                    ShiftType = shift,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }

        return period;
    }
}
