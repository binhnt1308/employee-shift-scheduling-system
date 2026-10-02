using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.SchedulePeriods.Controllers;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.SchedulePeriods.ViewModels;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.SchedulePeriods;

public sealed class SchedulePeriodLifecycleTests
{
    [Fact(DisplayName =
        "TC-LC-019: Kỳ mới dùng cấu hình đang hoạt động, cấu hình mới sau đó không ảnh hưởng kỳ cũ")]
    public async Task CreateAsync_UsesActiveShifts_AndFutureShiftChangesDoNotAffectExistingPeriod()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = new FakeShiftTypeRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));

        SeedV1ShiftTypes(shiftTypeRepo);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, new FakeNotificationRepository(),
            new FakeAuditLogRepository(), unitOfWork, clock, NullLogger<SchedulePeriodService>.Instance);

        var model1 = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ 1",
            WeekStartDate = new DateOnly(2026, 9, 14),
            AvailabilityDeadline = new DateTime(2026, 9, 10, 12, 0, 0)
        };

        var result1 = await service.CreateAsync(model1, actorAccountId: 1);
        Assert.True(result1.Succeeded);

        var period1 = periodRepo.SchedulePeriods[0];
        Assert.Equal(21, period1.StaffingRequirements.Count);
        Assert.All(period1.StaffingRequirements, req => Assert.Contains(req.ShiftTypeId, new[] { 1, 2, 3 }));

        ReplaceWithV2ShiftTypes(shiftTypeRepo);

        Assert.All(period1.StaffingRequirements, req => Assert.Contains(req.ShiftTypeId, new[] { 1, 2, 3 }));

        var model2 = new SchedulePeriodCreateViewModel
        {
            PeriodName = "Kỳ 2",
            WeekStartDate = new DateOnly(2026, 9, 21),
            AvailabilityDeadline = new DateTime(2026, 9, 17, 12, 0, 0)
        };

        var result2 = await service.CreateAsync(model2, actorAccountId: 1);
        Assert.True(result2.Succeeded);

        var period2 = periodRepo.SchedulePeriods[1];
        Assert.Equal(21, period2.StaffingRequirements.Count);
        Assert.All(period2.StaffingRequirements, req => Assert.Contains(req.ShiftTypeId, new[] { 4, 5, 6 }));
    }

    private static void SeedV1ShiftTypes(FakeShiftTypeRepository shiftTypeRepo)
    {
        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng",
            StartTime = new(8, 0), EndTime = new(12, 0),
            ConfigurationVersion = 1, IsActive = true, DisplayOrder = 1
        });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều",
            StartTime = new(12, 0), EndTime = new(16, 0),
            ConfigurationVersion = 1, IsActive = true, DisplayOrder = 2
        });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối",
            StartTime = new(16, 0), EndTime = new(20, 0),
            ConfigurationVersion = 1, IsActive = true, DisplayOrder = 3
        });
    }

    private static void ReplaceWithV2ShiftTypes(FakeShiftTypeRepository shiftTypeRepo)
    {
        foreach (var s in shiftTypeRepo.ShiftTypes)
        {
            s.IsActive = false;
        }
        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 4, ShiftCode = "MORNING", ShiftName = "Ca sáng mới",
            StartTime = new(7, 30), EndTime = new(11, 30),
            ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1
        });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 5, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều mới",
            StartTime = new(11, 30), EndTime = new(15, 30),
            ConfigurationVersion = 2, IsActive = true, DisplayOrder = 2
        });
        shiftTypeRepo.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 6, ShiftCode = "EVENING", ShiftName = "Ca tối mới",
            StartTime = new(15, 30), EndTime = new(19, 30),
            ConfigurationVersion = 2, IsActive = true, DisplayOrder = 3
        });
    }

    [Fact(DisplayName =
        "TC-LC-022: GetIndexAsync dữ liệu cũ sai -> Hiển thị 'Đã đóng đăng ký', CanSchedule = true")]
    public async Task GetIndexAsync_WhenPeriodStartedWithFutureDeadlineAndOpenStatus_DisplaysClosedAndCanSchedule()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var workScheduleRepo = new FakeWorkScheduleRepository();

        var initialDeadline = new DateTime(2026, 9, 6, 23, 59, 0, DateTimeKind.Utc);
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1, PeriodName = "Kỳ 31/08 - 06/09",
            WeekStartDate = new DateOnly(2026, 8, 31), WeekEndDate = new DateOnly(2026, 9, 6),
            AvailabilityDeadline = initialDeadline, RegistrationStatus = RegistrationStatus.Open
        };
        periodRepo.Add(period);

        var clock = new FakeClock(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
        var service = CreateWorkScheduleService(workScheduleRepo, periodRepo, clock);

        var indexModel = await service.GetIndexAsync(1, 10);
        var item = Assert.Single(indexModel.Periods);

        Assert.NotEqual("Đang mở đăng ký", item.EffectiveRegistrationStatusName);
        Assert.Equal("Đã đóng đăng ký", item.EffectiveRegistrationStatusName);
        Assert.Equal("badge bg-dark", item.EffectiveRegistrationBadgeClass);
        Assert.False(item.IsRegistrationEffectivelyOpen);
        Assert.True(item.CanSchedule);

        Assert.Empty(workScheduleRepo.Schedules);
        var editModel = await service.GetEditGridAsync(1, 100);
        Assert.NotNull(editModel);
        Assert.Single(workScheduleRepo.Schedules);
        Assert.Equal(1, workScheduleRepo.Schedules[0].SchedulePeriodId);

        Assert.Equal(RegistrationStatus.Open, period.RegistrationStatus);
        Assert.Equal(initialDeadline, period.AvailabilityDeadline);
    }

    [Fact(DisplayName =
        "TC-LC-023: GetIndexAsync kỳ tương lai mở -> Hiển thị 'Đang mở đăng ký', CanSchedule = false")]
    public async Task GetIndexAsync_WhenUpcomingPeriodOpen_DisplaysOpenAndCannotScheduleWithoutDraft()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var workScheduleRepo = new FakeWorkScheduleRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 2, PeriodName = "Kỳ tương lai",
            WeekStartDate = new DateOnly(2026, 9, 14), WeekEndDate = new DateOnly(2026, 9, 20),
            AvailabilityDeadline = new DateTime(2026, 9, 12, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        periodRepo.Add(period);

        var clock = new FakeClock(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
        var service = CreateWorkScheduleService(workScheduleRepo, periodRepo, clock);

        var indexModel = await service.GetIndexAsync(1, 10);
        var item = Assert.Single(indexModel.Periods);

        Assert.Equal("Đang mở đăng ký", item.EffectiveRegistrationStatusName);
        Assert.Equal("badge bg-success", item.EffectiveRegistrationBadgeClass);
        Assert.True(item.IsRegistrationEffectivelyOpen);
        Assert.False(item.CanSchedule);

        var editModel = await service.GetEditGridAsync(2, 100);
        Assert.Null(editModel);
        Assert.Empty(workScheduleRepo.Schedules);
    }

    [Fact(DisplayName =
        "TC-LC-024: GetIndexAsync kỳ Preparing -> Hiển thị 'Đang chuẩn bị', CanSchedule = false")]
    public async Task GetIndexAsync_WhenPreparingPeriod_DisplaysPreparingAndCannotSchedule()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var workScheduleRepo = new FakeWorkScheduleRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 3, PeriodName = "Kỳ đang chuẩn bị",
            WeekStartDate = new DateOnly(2026, 9, 14), WeekEndDate = new DateOnly(2026, 9, 20),
            AvailabilityDeadline = new DateTime(2026, 9, 12, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Preparing
        };
        periodRepo.Add(period);

        var clock = new FakeClock(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
        var service = CreateWorkScheduleService(workScheduleRepo, periodRepo, clock);

        var indexModel = await service.GetIndexAsync(1, 10);
        var item = Assert.Single(indexModel.Periods);

        Assert.Equal("Đang chuẩn bị", item.EffectiveRegistrationStatusName);
        Assert.Equal("badge bg-secondary", item.EffectiveRegistrationBadgeClass);
        Assert.False(item.IsRegistrationEffectivelyOpen);
        Assert.False(item.CanSchedule);

        var editModel = await service.GetEditGridAsync(3, 100);
        Assert.Null(editModel);
        Assert.Empty(workScheduleRepo.Schedules);
    }

    [Fact(DisplayName = "TC-LC-025: SchedulePeriodController.Create GET tính default Next Monday và Sunday deadline")]
    public void Create_DefaultGetAction_EnsuresNextMondayAndSundayDeadline_ForWednesdaySundayMonday()
    {
        {
            var clockWed = new FakeClock(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
            var controller = new SchedulePeriodController(null!, clockWed);
            var result = Assert.IsType<ViewResult>(controller.Create());
            var model = Assert.IsType<SchedulePeriodCreateViewModel>(result.Model);

            Assert.Equal(new DateOnly(2026, 9, 7), model.WeekStartDate);
            Assert.Equal(new DateTime(2026, 9, 6, 23, 59, 0), model.AvailabilityDeadline);
            Assert.True(DateOnly.FromDateTime(model.AvailabilityDeadline) < model.WeekStartDate);
        }

        {
            var clockSun = new FakeClock(new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc));
            var controller = new SchedulePeriodController(null!, clockSun);
            var result = Assert.IsType<ViewResult>(controller.Create());
            var model = Assert.IsType<SchedulePeriodCreateViewModel>(result.Model);

            Assert.Equal(new DateOnly(2026, 9, 7), model.WeekStartDate);
            Assert.Equal(new DateTime(2026, 9, 6, 23, 59, 0), model.AvailabilityDeadline);
            Assert.True(DateOnly.FromDateTime(model.AvailabilityDeadline) < model.WeekStartDate);
        }

        {
            var clockMon = new FakeClock(new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc));
            var controller = new SchedulePeriodController(null!, clockMon);
            var result = Assert.IsType<ViewResult>(controller.Create());
            var model = Assert.IsType<SchedulePeriodCreateViewModel>(result.Model);

            Assert.Equal(new DateOnly(2026, 9, 14), model.WeekStartDate);
            Assert.Equal(new DateTime(2026, 9, 13, 23, 59, 0), model.AvailabilityDeadline);
            Assert.True(DateOnly.FromDateTime(model.AvailabilityDeadline) < model.WeekStartDate);
        }
    }

    [Fact(DisplayName = "TC-LC-026: Kỳ tương lai có draft -> CanSchedule = true, GetEditGrid trả model")]
    public async Task GetEditGridAsync_WhenUpcomingPeriodOpenWithExistingDraft_ReturnsModelAndDoesNotCreateNewDraft()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var workScheduleRepo = new FakeWorkScheduleRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 4, PeriodName = "Kỳ tương lai có draft",
            WeekStartDate = new DateOnly(2026, 9, 14), WeekEndDate = new DateOnly(2026, 9, 20),
            AvailabilityDeadline = new DateTime(2026, 9, 12, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        periodRepo.Add(period);

        var existingDraft = new WorkSchedule
        {
            WorkScheduleId = 40, SchedulePeriodId = 4, Status = ScheduleStatus.Draft
        };
        workScheduleRepo.Schedules.Add(existingDraft);

        var clock = new FakeClock(new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc));
        var service = CreateWorkScheduleService(workScheduleRepo, periodRepo, clock);

        var indexModel = await service.GetIndexAsync(1, 10);
        var item = Assert.Single(indexModel.Periods);
        Assert.True(item.CanSchedule);

        var editModel = await service.GetEditGridAsync(4, 100);
        Assert.NotNull(editModel);
        Assert.Equal(40, editModel.WorkScheduleId);
        Assert.Single(workScheduleRepo.Schedules);
    }

    [Fact(DisplayName = "TC-LC-027: Details kỳ có lịch Published trước tuần bắt đầu -> 'Đã công bố'")]
    public async Task GetDetailsAsync_WhenPublishedScheduleBeforeStart_ReturnsDaCongBoStatus()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateFixedShiftTypeRepository();
        var clock = new FakeClock(new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc));

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 5, PeriodName = "Kỳ 21/09 - 27/09",
            WeekStartDate = new DateOnly(2026, 9, 21), WeekEndDate = new DateOnly(2026, 9, 27),
            AvailabilityDeadline = new DateTime(2026, 9, 19, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Closed
        };
        var publishedSchedule = new WorkSchedule
        {
            WorkScheduleId = 50, SchedulePeriodId = 5, Status = ScheduleStatus.Published
        };
        period.WorkSchedules.Add(publishedSchedule);
        periodRepo.Add(period);

        var service = new SchedulePeriodService(
            periodRepo, shiftTypeRepo, new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock,
            NullLogger<SchedulePeriodService>.Instance);

        var details = await service.GetDetailsAsync(5);

        Assert.NotNull(details);
        Assert.Equal("Đã công bố", details.EffectiveStatusName);
        Assert.Equal("badge-success", details.DisplayBadgeClass);
    }

    [Fact(DisplayName = "TC-LC-028: Edit kỳ kết thúc có lịch Published chưa chốt -> 'Đang áp dụng' & CanClose")]
    public async Task GetEditGridAsync_WhenEndedPeriodPublishedBeforeClose_ReturnsDangApDungAndCanClose()
    {
        var periodRepo = new FakeSchedulePeriodRepository();
        var workScheduleRepo = new FakeWorkScheduleRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 6, PeriodName = "Kỳ 07/09 - 13/09",
            WeekStartDate = new DateOnly(2026, 9, 7), WeekEndDate = new DateOnly(2026, 9, 13),
            AvailabilityDeadline = new DateTime(2026, 9, 5, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Closed
        };
        var schedule = new WorkSchedule
        {
            WorkScheduleId = 60, SchedulePeriodId = 6, Status = ScheduleStatus.Published
        };
        period.WorkSchedules.Add(schedule);
        periodRepo.Add(period);
        workScheduleRepo.Schedules.Add(schedule);

        var clock = new FakeClock(new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc));
        var service = CreateWorkScheduleService(workScheduleRepo, periodRepo, clock);

        var editModel = await service.GetEditGridAsync(6, 100);

        Assert.NotNull(editModel);
        Assert.Equal("Đang áp dụng", editModel.EffectiveStatusName);
        Assert.Equal("badge-active", editModel.DisplayBadgeClass);
        Assert.True(editModel.CanClose);
        Assert.False(editModel.CanEditAssignments);
    }

    private static WorkScheduleService CreateWorkScheduleService(
        FakeWorkScheduleRepository workScheduleRepo,
        FakeSchedulePeriodRepository periodRepo,
        FakeClock clock,
        FakeShiftTypeRepository? shiftTypeRepo = null)
    {
        var validationService = new SchedulingValidationService();
        var orToolsService = new OrToolsSchedulingService(
            validationService, NullLogger<OrToolsSchedulingService>.Instance);
        var shiftRepo = shiftTypeRepo ?? CreateFixedShiftTypeRepository();

        return new WorkScheduleService(
            workScheduleRepo, periodRepo, shiftRepo,
            new FakeEmployeeRepository(), new FakeAvailabilityRepository(), new FakeLeaveRequestRepository(),
            validationService, orToolsService, new FakeNotificationRepository(),
            new FakeAuditLogRepository(), new FakeUnitOfWork(), clock, NullLogger<WorkScheduleService>.Instance);
    }

    private static FakeShiftTypeRepository CreateFixedShiftTypeRepository()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.AddRange(new[]
        {
            new ShiftType
            {
                ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng",
                StartTime = new(8, 0), EndTime = new(12, 0),
                ConfigurationVersion = 1, IsActive = true, DisplayOrder = 1
            },
            new ShiftType
            {
                ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều",
                StartTime = new(12, 0), EndTime = new(16, 0),
                ConfigurationVersion = 1, IsActive = true, DisplayOrder = 2
            },
            new ShiftType
            {
                ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối",
                StartTime = new(16, 0), EndTime = new(20, 0),
                ConfigurationVersion = 1, IsActive = true, DisplayOrder = 3
            }
        });
        return repo;
    }
}
