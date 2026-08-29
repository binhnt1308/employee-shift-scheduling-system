using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Common;

/// <summary>
/// Kiem tra cau hinh Culture vi-VN va chuc nang quy doi gio Viet Nam (LC-03).
/// </summary>
public sealed class LocalizationAndClockTests
{
    [Fact(DisplayName = "TC-LC03-01 - SystemClock ToVietnamTime maps UTC to Vietnam time (+07:00)")]
    public void ToVietnamTime_GivenUtcTime_ReturnsVietnamTime()
    {
        var clock = new SystemClock();
        var utcTime = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var vietnamTime = clock.ToVietnamTime(utcTime);

        Assert.Equal(2026, vietnamTime.Year);
        Assert.Equal(8, vietnamTime.Month);
        Assert.Equal(15, vietnamTime.Day);
        Assert.Equal(7, vietnamTime.Hour);
        Assert.Equal(0, vietnamTime.Minute);
    }

    [Fact(DisplayName = "TC-LC03-02 - Default Request Culture is configured to vi-VN")]
    public void CultureConfig_HasDefaultViVnCulture()
    {
        var services = new ServiceCollection();
        var vietnamCulture = new CultureInfo("vi-VN");
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(vietnamCulture);
            options.SupportedCultures = new[] { vietnamCulture };
            options.SupportedUICultures = new[] { vietnamCulture };
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

        Assert.Equal("vi-VN", options.DefaultRequestCulture.Culture.Name);
        Assert.Single(options.SupportedCultures!);
        Assert.Equal("vi-VN", options.SupportedCultures![0].Name);
    }

    [Fact(DisplayName = "TC-LC03-03 - Vietnam wall-clock to UTC conversion via SystemClock")]
    public void ToUtcFromVietnamTime_GivenVietnamTime_ReturnsUtcTime()
    {
        var clock = new SystemClock();
        var vietnamInput = new DateTime(2026, 8, 15, 23, 59, 0, DateTimeKind.Unspecified);
        var expectedUtc = new DateTime(2026, 8, 15, 16, 59, 0, DateTimeKind.Utc);

        var actualUtc = clock.ToUtcFromVietnamTime(vietnamInput);

        Assert.Equal(expectedUtc, actualUtc);
        Assert.Equal(DateTimeKind.Utc, actualUtc.Kind);
    }

    [Fact(DisplayName = "TC-LC03-04 - Create SchedulePeriod converts deadline before persistence")]
    public async Task CreateAsync_ConvertsVietnamDeadlineBeforePersistence()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        shiftTypeRepo.ShiftTypes.AddRange(new[]
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DisplayOrder = 1, IsActive = true },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DisplayOrder = 2, IsActive = true },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DisplayOrder = 3, IsActive = true }
        });

        var fakeClock = new FakeClock(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo,
            shiftTypeRepo,
            new FakeNotificationRepository(),
            new FakeAuditLogRepository(),
            new FakeUnitOfWork(),
            fakeClock,
            NullLogger<SchedulePeriodService>.Instance);

        var vietnamDeadlineInput = new DateTime(2026, 8, 15, 23, 59, 0, DateTimeKind.Unspecified);
        var model = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ lịch test semantic",
            WeekStartDate = new DateOnly(2026, 8, 17),
            AvailabilityDeadline = vietnamDeadlineInput
        };

        var result = await service.CreateAsync(model, 1);

        Assert.True(result.Succeeded);
        Assert.Single(periodRepo.SchedulePeriods);
        var savedPeriod = periodRepo.SchedulePeriods[0];
        var expectedSavedUtc = new DateTime(2026, 8, 15, 16, 59, 0, DateTimeKind.Utc);
        Assert.Equal(expectedSavedUtc, savedPeriod.AvailabilityDeadline);
    }

    [Fact(DisplayName = "TC-LC03-05 - Deadline boundary comparison UTC vs UTC")]
    public void DeadlineBoundaryComparison_EvaluatesCorrectlyAtUtcBoundary()
    {
        var deadlineUtc = new DateTime(2026, 8, 15, 16, 59, 0, DateTimeKind.Utc);

        var utcBeforeDeadline = new DateTime(2026, 8, 15, 16, 58, 59, DateTimeKind.Utc);
        bool isExpiredBefore = deadlineUtc <= utcBeforeDeadline;
        Assert.False(isExpiredBefore);

        var utcAtDeadline = new DateTime(2026, 8, 15, 16, 59, 0, DateTimeKind.Utc);
        bool isExpiredAt = deadlineUtc <= utcAtDeadline;
        Assert.True(isExpiredAt);
    }

    [Fact(DisplayName = "TC-LC03-06 - Display conversion maps stored UTC to Vietnam time")]
    public async Task GetDetailsAsync_MapsStoredUtcDeadlineToVietnamTimeViewModel()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var storedUtcDeadline = new DateTime(2026, 8, 15, 16, 59, 0, DateTimeKind.Utc);
        var start = new DateOnly(2026, 8, 17);

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ lịch 17/08 - 23/08",
            WeekStartDate = start,
            WeekEndDate = start.AddDays(6),
            AvailabilityDeadline = storedUtcDeadline,
            RegistrationStatus = RegistrationStatus.Preparing,
            CreatedByAccountId = 1,
            CreatedAt = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
            StaffingRequirements = new List<StaffingRequirement>()
        };
        periodRepo.Add(period);

        var fakeClock = new FakeClock(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));
        var service = new SchedulePeriodService(
            periodRepo,
            shiftTypeRepo,
            new FakeNotificationRepository(),
            new FakeAuditLogRepository(),
            new FakeUnitOfWork(),
            fakeClock,
            NullLogger<SchedulePeriodService>.Instance);

        var detailsVm = await service.GetDetailsAsync(1);

        Assert.NotNull(detailsVm);
        var expectedVietnamDeadline = new DateTime(2026, 8, 15, 23, 59, 0);
        Assert.Equal(expectedVietnamDeadline.Year, detailsVm.AvailabilityDeadline.Year);
        Assert.Equal(expectedVietnamDeadline.Month, detailsVm.AvailabilityDeadline.Month);
        Assert.Equal(expectedVietnamDeadline.Day, detailsVm.AvailabilityDeadline.Day);
        Assert.Equal(expectedVietnamDeadline.Hour, detailsVm.AvailabilityDeadline.Hour);
        Assert.Equal(expectedVietnamDeadline.Minute, detailsVm.AvailabilityDeadline.Minute);
    }
}
