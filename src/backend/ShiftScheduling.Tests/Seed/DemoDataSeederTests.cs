using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.SchedulePeriods.Services;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Infrastructure.Persistence.Seed;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Seed;

public sealed class DemoDataSeederTests
{
    private readonly IPasswordHashService _passwordHashService = new Pbkdf2PasswordHashService();
    private readonly IClock _clock =
        new FakeClock(new DateTime(2026, 9, 11, 4, 0, 0, DateTimeKind.Utc));
    private readonly SchedulingValidationService _validationService = new();

    [Fact(DisplayName = "TC-01: Dataset tạo đủ 8 nhân viên từ NV01 đến NV08")]
    public void TC01_BuildDemoDataSet_CreatesNV01ToNV08()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        Assert.NotNull(dataSet);
        Assert.Equal(8, dataSet.Employees.Count);

        var expectedCodes = new[] { "NV01", "NV02", "NV03", "NV04", "NV05", "NV06", "NV07", "NV08" };
        foreach (var code in expectedCodes)
        {
            Assert.Contains(dataSet.Employees, e => e.EmployeeCode == code);
        }
    }

    [Fact(DisplayName = "TC-02: Không còn tiền tố DEMO_EMPxx hay username demo_*")]
    public void TC02_BuildDemoDataSet_DoesNotContainDemoEmpPrefixOrDemoUsernames()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        Assert.DoesNotContain(dataSet.Employees, e => e.EmployeeCode.StartsWith("DEMO_"));
        Assert.DoesNotContain(dataSet.Accounts, a => a.Username.StartsWith("demo_"));
        Assert.DoesNotContain(dataSet.Employees, e => e.EmployeeCode == "EMP001");
    }

    [Fact(DisplayName = "TC-03: NV02 chính xác là Chiêm Trung Hậu - Nhân viên demo chính")]
    public void TC03_NV02_IsChiemTrungHau_MainDemoEmployee()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var nv02 = dataSet.Employees.Single(e => e.EmployeeCode == "NV02");
        Assert.Equal("Chiêm Trung Hậu", nv02.FullName);
        Assert.Equal("2351050042hau@ou.edu.vn", nv02.Email);
        Assert.NotNull(nv02.UserAccount);
        Assert.Equal("nv02", nv02.UserAccount!.Username);
    }

    [Fact(DisplayName = "TC-04: NV02 có đúng cấu hình Position = Sales, EmploymentType = FullTime, Level = Level2")]
    public void TC04_NV02_HasCorrectPositionEmploymentTypeLevel()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var nv02 = dataSet.Employees.Single(e => e.EmployeeCode == "NV02");
        Assert.Equal(EmployeePosition.Sales, nv02.Position);
        Assert.Equal(EmploymentType.FullTime, nv02.EmploymentType);
        Assert.Equal(EmployeeLevel.Level2, nv02.Level);
        Assert.True(nv02.IsActive);
    }

    [Fact(DisplayName = "TC-05: Cơ cấu nhân sự gồm đúng 4 Sales và 4 Cashier")]
    public void TC05_PositionBreakdown_Has4SalesAnd4Cashiers()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var sales = dataSet.Employees.Where(e => e.Position == EmployeePosition.Sales).ToList();
        var cashiers = dataSet.Employees.Where(e => e.Position == EmployeePosition.Cashier).ToList();

        Assert.Equal(4, sales.Count);
        Assert.Equal(4, cashiers.Count);

        Assert.Contains(sales, e => e.EmployeeCode == "NV01");
        Assert.Contains(sales, e => e.EmployeeCode == "NV02");
        Assert.Contains(sales, e => e.EmployeeCode == "NV04");
        Assert.Contains(sales, e => e.EmployeeCode == "NV08");

        Assert.Contains(cashiers, e => e.EmployeeCode == "NV03");
        Assert.Contains(cashiers, e => e.EmployeeCode == "NV05");
        Assert.Contains(cashiers, e => e.EmployeeCode == "NV06");
        Assert.Contains(cashiers, e => e.EmployeeCode == "NV07");
    }

    [Fact(DisplayName = "TC-06: Cơ cấu nhân sự gồm đúng 5 FullTime và 3 PartTime")]
    public void TC06_EmploymentTypeBreakdown_Has5FullTimeAnd3PartTime()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var ft = dataSet.Employees.Where(e => e.EmploymentType == EmploymentType.FullTime).ToList();
        var pt = dataSet.Employees.Where(e => e.EmploymentType == EmploymentType.PartTime).ToList();

        Assert.Equal(5, ft.Count);
        Assert.Equal(3, pt.Count);

        // Nhóm toàn thời gian gồm 5 nhân viên: NV01, NV02, NV03, NV04, NV07.
        Assert.Contains(ft, e => e.EmployeeCode == "NV01");
        Assert.Contains(ft, e => e.EmployeeCode == "NV02");
        Assert.Contains(ft, e => e.EmployeeCode == "NV03");
        Assert.Contains(ft, e => e.EmployeeCode == "NV04");
        Assert.Contains(ft, e => e.EmployeeCode == "NV07");

        // Nhóm bán thời gian gồm 3 nhân viên: NV05, NV06, NV08.
        Assert.Contains(pt, e => e.EmployeeCode == "NV05");
        Assert.Contains(pt, e => e.EmployeeCode == "NV06");
        Assert.Contains(pt, e => e.EmployeeCode == "NV08");
    }

    [Fact(DisplayName = "TC-07: Scenario A tồn tại và trạng thái Registration mở trong tương lai")]
    public void TC07_ScenarioA_Exists_AndRegistrationIsOpenInFuture()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodA = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.RegistrationDemoPeriodMarker));
        Assert.Equal(RegistrationStatus.Open, periodA.RegistrationStatus);
        Assert.True(periodA.WeekStartDate > today, "Scenario A phải bắt đầu trong tương lai.");
        Assert.Equal(6, periodA.WeekEndDate.DayNumber - periodA.WeekStartDate.DayNumber);
        Assert.True(periodA.AvailabilityDeadline > _clock.UtcNow, "Deadline phải lớn hơn thời điểm hiện tại.");
        Assert.Contains(DemoDataSeeder.RegistrationDemoPeriodMarker, periodA.PeriodName);
    }

    [Fact(DisplayName = "TC-07B: Scenario Ready tồn tại, deadline đã qua, IsRegistrationEffectivelyOpen == false, chưa có WorkSchedule/Assignment")]
    public void TC07B_ScenarioReady_Exists_AndRegistrationIsEffectivelyClosed()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodReady = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.ReadyToScheduleDemoPeriodMarker));
        Assert.Equal(RegistrationStatus.Open, periodReady.RegistrationStatus);
        Assert.True(periodReady.WeekStartDate > today, "Kỳ sẵn sàng xếp lịch phải bắt đầu ở tuần tới.");
        Assert.Equal(6, periodReady.WeekEndDate.DayNumber - periodReady.WeekStartDate.DayNumber);
        Assert.True(periodReady.AvailabilityDeadline < _clock.UtcNow, "Deadline đăng ký phải đã qua.");
        Assert.False(SchedulePeriodStatusHelper.IsRegistrationEffectivelyOpen(periodReady, _clock.UtcNow, today));
        Assert.DoesNotContain(dataSet.WorkSchedules, ws => ws.SchedulePeriod == periodReady);
        Assert.DoesNotContain(dataSet.ShiftAssignments, sa => sa.WorkDate >= periodReady.WeekStartDate && sa.WorkDate <= periodReady.WeekEndDate);
    }

    [Fact(DisplayName = "TC-08: Scenario A có dữ liệu Availability cả Normal và Preferred")]
    public void TC08_ScenarioA_HasNormalAndPreferredAvailabilities()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodA = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.RegistrationDemoPeriodMarker));
        var availsA = dataSet.Availabilities.Where(a => a.SchedulePeriod == periodA).ToList();

        Assert.NotEmpty(availsA);
        Assert.Contains(availsA, a => a.PreferenceLevel == AvailabilityPreferenceLevel.Normal);
        Assert.Contains(availsA, a => a.PreferenceLevel == AvailabilityPreferenceLevel.Preferred);

        // Mọi nhân viên NV01..NV08 đều có lịch rảnh trong Period A
        foreach (var emp in dataSet.Employees)
        {
            Assert.Contains(availsA, a => a.Employee == emp);
        }
    }

    [Fact(DisplayName = "TC-08B: Scenario Ready có dữ liệu Availability cả Normal và Preferred")]
    public void TC08B_ScenarioReady_HasNormalAndPreferredAvailabilities()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodReady = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.ReadyToScheduleDemoPeriodMarker));
        var availsReady = dataSet.Availabilities.Where(a => a.SchedulePeriod == periodReady).ToList();

        Assert.NotEmpty(availsReady);
        Assert.Contains(availsReady, a => a.PreferenceLevel == AvailabilityPreferenceLevel.Normal);
        Assert.Contains(availsReady, a => a.PreferenceLevel == AvailabilityPreferenceLevel.Preferred);

        // Mọi nhân viên NV01..NV08 đều có lịch rảnh trong Period Ready
        foreach (var emp in dataSet.Employees)
        {
            Assert.Contains(availsReady, a => a.Employee == emp);
        }
    }

    [Fact(DisplayName = "TC-09: Scenario A có đúng 21 bản ghi StaffingRequirement")]
    public void TC09_ScenarioA_HasStaffingRequirements_21Records()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodA = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.RegistrationDemoPeriodMarker));
        var reqsA = dataSet.StaffingRequirements.Where(r => r.SchedulePeriod == periodA).ToList();

        Assert.Equal(21, reqsA.Count);
        Assert.All(reqsA, r =>
        {
            Assert.Equal(1, r.RequiredSalesStaff);
            Assert.Equal(1, r.RequiredCashiers);
            Assert.Equal(1, r.RequiredExperiencedStaff);
            Assert.Equal(EmployeeLevel.Level2, r.MinimumLevel);
        });
    }

    [Fact(DisplayName = "TC-09B: Scenario Ready có đúng 21 bản ghi StaffingRequirement")]
    public void TC09B_ScenarioReady_HasStaffingRequirements_21Records()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodReady = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.ReadyToScheduleDemoPeriodMarker));
        var reqsReady = dataSet.StaffingRequirements.Where(r => r.SchedulePeriod == periodReady).ToList();

        Assert.Equal(21, reqsReady.Count);
        Assert.All(reqsReady, r =>
        {
            Assert.Equal(1, r.RequiredSalesStaff);
            Assert.Equal(1, r.RequiredCashiers);
            Assert.Equal(1, r.RequiredExperiencedStaff);
            Assert.Equal(EmployeeLevel.Level2, r.MinimumLevel);
        });
    }

    [Fact(DisplayName = "TC-10: Scenario A giải thành công bằng OR-Tools CP-SAT (Feasible/Optimal)")]
    public void TC10_ScenarioA_Availabilities_AreFeasibleForOrToolsSolver()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodA = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.RegistrationDemoPeriodMarker));
        var reqsA = dataSet.StaffingRequirements.Where(r => r.SchedulePeriod == periodA).ToList();
        var availsA = dataSet.Availabilities.Where(a => a.SchedulePeriod == periodA).ToList();

        for (int i = 0; i < dataSet.Employees.Count; i++)
        {
            dataSet.Employees[i].EmployeeId = i + 1;
        }
        foreach (var av in availsA)
        {
            av.EmployeeId = av.Employee.EmployeeId;
        }

        var shiftTypes = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1, DisplayOrder = 1 },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DurationHours = 4, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1, DisplayOrder = 2 },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1, DisplayOrder = 3 }
        };

        var orToolsService = new OrToolsSchedulingService(_validationService, Microsoft.Extensions.Logging.Abstractions.NullLogger<OrToolsSchedulingService>.Instance);
        var proposal = orToolsService.GenerateProposal(
            periodA,
            dataSet.Employees,
            availsA,
            new List<LeaveRequest>(),
            reqsA,
            shiftTypes,
            timeoutSeconds: 5);

        Assert.True(proposal.Succeeded, $"OR-Tools failed with status {proposal.Status}: {proposal.Message}");
        Assert.NotEmpty(proposal.ProposedAssignments);
        Assert.True(proposal.ValidationResult.IsValid);
        Assert.Empty(proposal.ValidationResult.HardErrors);
    }

    [Fact(DisplayName = "TC-10B: Scenario Ready giải thành công bằng OR-Tools CP-SAT (Feasible/Optimal)")]
    public void TC10B_ScenarioReady_Availabilities_AreFeasibleForOrToolsSolver()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodReady = dataSet.Periods.Single(p => p.PeriodName.Contains(DemoDataSeeder.ReadyToScheduleDemoPeriodMarker));
        var reqsReady = dataSet.StaffingRequirements.Where(r => r.SchedulePeriod == periodReady).ToList();
        var availsReady = dataSet.Availabilities.Where(a => a.SchedulePeriod == periodReady).ToList();

        for (int i = 0; i < dataSet.Employees.Count; i++)
        {
            dataSet.Employees[i].EmployeeId = i + 1;
        }
        foreach (var av in availsReady)
        {
            av.EmployeeId = av.Employee.EmployeeId;
        }

        var shiftTypes = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1, DisplayOrder = 1 },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DurationHours = 4, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1, DisplayOrder = 2 },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1, DisplayOrder = 3 }
        };

        var orToolsService = new OrToolsSchedulingService(_validationService, Microsoft.Extensions.Logging.Abstractions.NullLogger<OrToolsSchedulingService>.Instance);
        var proposal = orToolsService.GenerateProposal(
            periodReady,
            dataSet.Employees,
            availsReady,
            new List<LeaveRequest>(),
            reqsReady,
            shiftTypes,
            timeoutSeconds: 5);

        Assert.True(proposal.Succeeded, $"OR-Tools failed with status {proposal.Status}: {proposal.Message}");
        Assert.NotEmpty(proposal.ProposedAssignments);
        Assert.True(proposal.ValidationResult.IsValid);
        Assert.Empty(proposal.ValidationResult.HardErrors);
    }

    [Fact(DisplayName = "TC-11: Scenario B đã Published và kết thúc trong quá khứ")]
    public void TC11_ScenarioB_IsPublished_WithValidTimestamps()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodB = dataSet.Periods.Single(p => p.RegistrationStatus == RegistrationStatus.Closed);
        Assert.True(periodB.WeekEndDate < today, "Scenario B phải kết thúc trong quá khứ.");

        var wsB = dataSet.WorkSchedules.Single(w => w.SchedulePeriod == periodB);
        Assert.Equal(ScheduleStatus.Published, wsB.Status);
        Assert.NotNull(wsB.PublishedAt);
        Assert.True(wsB.IsGeneratedByORTools);
    }

    [Fact(DisplayName = "TC-12: Scenario B có đúng 42 lượt phân công ShiftAssignments")]
    public void TC12_ScenarioB_Has42ShiftAssignments()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodB = dataSet.Periods.Single(p => p.RegistrationStatus == RegistrationStatus.Closed);
        var wsB = dataSet.WorkSchedules.Single(w => w.SchedulePeriod == periodB);
        var assignsB = dataSet.ShiftAssignments.Where(a => a.WorkSchedule == wsB).ToList();

        Assert.Equal(42, assignsB.Count);
        Assert.All(assignsB, a =>
        {
            Assert.Contains(a.Employee, dataSet.Employees);
            Assert.InRange(a.ShiftTypeId, 1, 3);
            Assert.InRange(a.WorkDate, periodB.WeekStartDate, periodB.WeekEndDate);
        });
    }

    [Fact(DisplayName = "TC-13: My Schedule của NV02 (Chiêm Trung Hậu) có dữ liệu đầy đủ")]
    public void TC13_MySchedule_ForNV02_HasData()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var nv02 = dataSet.Employees.Single(e => e.EmployeeCode == "NV02");
        var nv02Assigns = dataSet.ShiftAssignments.Where(a => a.Employee == nv02).ToList();

        Assert.NotEmpty(nv02Assigns);
        Assert.Equal(6, nv02Assigns.Count);
    }

    [Fact(DisplayName = "TC-14: NC-07 NV02 Thứ Sáu ca EVENING có ActualWorkedHours = 3.50h hợp lệ trong khoảng [0, DurationHours] và khác giờ tính toán")]
    public void TC14_NC07_NV02_FridayEvening_HasValidActualWorkedHours()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodB = dataSet.Periods.Single(p => p.RegistrationStatus == RegistrationStatus.Closed);
        var friday = periodB.WeekStartDate.AddDays(4);

        var nv02 = dataSet.Employees.Single(e => e.EmployeeCode == "NV02");
        var fridayEveningAssign = dataSet.ShiftAssignments.Single(
            a => a.Employee == nv02 && a.WorkDate == friday && a.ShiftTypeId == 3);

        const decimal shiftDurationHours = 4.00m; // Ca EVENING chuẩn có thời lượng 4.00h
        const decimal systemCalculatedHours = 4.00m;

        // 1. Phân công đích của kịch bản phải thuộc NV02 - Chiêm Trung Hậu.
        Assert.Equal("NV02", fridayEveningAssign.Employee.EmployeeCode);

        // 2. ActualWorkedHours phải có giá trị
        Assert.NotNull(fridayEveningAssign.ActualWorkedHours);
        var actualHours = fridayEveningAssign.ActualWorkedHours.Value;

        // 3. Giờ công thực tế luôn nằm trong khoảng từ 0 đến thời lượng ca.
        Assert.True(actualHours >= 0, "ActualWorkedHours phải lớn hơn hoặc bằng 0.");
        Assert.True(actualHours <= shiftDurationHours, "ActualWorkedHours không được vượt quá DurationHours của ca.");

        // 4. ActualWorkedHours != SystemCalculatedHours để thể hiện điều chỉnh thủ công
        Assert.NotEqual(systemCalculatedHours, actualHours);

        // 5. Giá trị cụ thể được seed
        Assert.Equal(3.50m, actualHours);
    }

    [Fact(DisplayName = "TC-15: NC-07 lý do điều chỉnh đúng chuẩn và không được rỗng")]
    public void TC15_NC07_Reason_IsNotEmptyAndMatchesAdjustment()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        var periodB = dataSet.Periods.Single(p => p.RegistrationStatus == RegistrationStatus.Closed);
        var friday = periodB.WeekStartDate.AddDays(4);

        var nv02 = dataSet.Employees.Single(e => e.EmployeeCode == "NV02");
        var fridayEveningAssign = dataSet.ShiftAssignments.Single(
            a => a.Employee == nv02 && a.WorkDate == friday && a.ShiftTypeId == 3);

        Assert.False(string.IsNullOrWhiteSpace(fridayEveningAssign.WorkdayAdjustmentReason));
        Assert.Equal("về sớm 30 phút theo điều chỉnh của quản lý", fridayEveningAssign.WorkdayAdjustmentReason);
    }

    [Fact(DisplayName = "TC-16: Idempotent - Complete demo dataset is recognized by IsAlreadySeeded")]
    public void TC16_Idempotency_CompleteDemoDataset_IsAlreadySeeded()
    {
        var completeNewPeriods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        // Khi đầy đủ tất cả thành phần demo mới 3 kỳ: trả về true
        Assert.True(DemoDataSeeder.IsAlreadySeeded(DemoDataSeeder.DemoEmployeeCodes, completeNewPeriods));

        var completeLegacyPeriods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đăng ký)"
        };

        // Khi database có bộ demo legacy 2 kỳ: cũng được nhận diện là đã seed và không seed lại
        Assert.True(DemoDataSeeder.IsAlreadySeeded(DemoDataSeeder.DemoEmployeeCodes, completeLegacyPeriods));

        // Khi có NV01-NV08 nhưng các kỳ không có marker demo (như ShiftSchedulingDb cũ): không throw và trả về false
        Assert.False(DemoDataSeeder.IsAlreadySeeded(DemoDataSeeder.DemoEmployeeCodes, new[] { "Kỳ lịch 1", "Kỳ lịch 31/08/2026" }));

        // Khi chỉ có nhân viên nghiệp vụ unrelated (không có demo indicator)
        Assert.False(DemoDataSeeder.IsAlreadySeeded(new[] { "EMP001", "EMP002" }, new[] { "Kỳ lịch tháng 8" }));
    }

    [Fact(DisplayName = "TC-16B: Post-demo user modifications on Ready/Registration => Precondition skips safely without throw")]
    public void TC16B_Precondition_PostDemoUserModifications_DoesNotThrowAndSkipsSafely()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        // 1. Khi người dùng đã thao tác tạo Draft và assignments trên Ready, sửa availability trên Registration
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingPeriodNames: periods);

        // EXPECTED: SkipAlreadySeeded an toàn, không ném InvalidOperationException
        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decision);
        Assert.True(DemoDataSeeder.IsAlreadySeeded(DemoDataSeeder.DemoEmployeeCodes, periods));

        // 2. Kể cả khi số lượng nhân viên bị thay đổi (ví dụ: nhân viên mới được thêm vào hoặc xóa bớt)
        var decisionWithDifferentEmployees = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 12,
            existingPeriodNames: periods);
        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decisionWithDifferentEmployees);
    }

    [Fact(DisplayName = "TC-17: Foreign Key và phân công Scenario B hợp lệ hoàn toàn theo SchedulingValidationService")]
    public void TC17_ForeignKeyAndNavigations_AreValid()
    {
        var today = new DateOnly(2026, 9, 11);
        var dataSet = DemoDataSeeder.BuildDemoDataSet(today, _passwordHashService, _clock);

        // 10 tài khoản: admin, quanly, nv01..nv08
        Assert.Equal(10, dataSet.Accounts.Count);
        Assert.Contains(dataSet.Accounts, a => a.Username == "admin");
        Assert.Contains(dataSet.Accounts, a => a.Username == "quanly");

        for (int i = 1; i <= 8; i++)
        {
            var username = $"nv0{i}";
            var empCode = $"NV0{i}";
            var account = dataSet.Accounts.Single(a => a.Username == username);
            var emp = dataSet.Employees.Single(e => e.EmployeeCode == empCode);

            Assert.Same(account, emp.UserAccount);
            Assert.True(_passwordHashService.VerifyPassword("Password123!", account.PasswordHash));
        }

        // Kiểm tra tính nhất quán của dữ liệu kịch bản B.
        var periodB = dataSet.Periods.Single(p => p.RegistrationStatus == RegistrationStatus.Closed);
        var reqsB = dataSet.StaffingRequirements.Where(r => r.SchedulePeriod == periodB).ToList();
        var availsB = dataSet.Availabilities.Where(a => a.SchedulePeriod == periodB).ToList();
        var assignmentsB = dataSet.ShiftAssignments.Where(a => a.WorkSchedule.SchedulePeriod == periodB).ToList();

        for (int i = 0; i < dataSet.Employees.Count; i++)
        {
            dataSet.Employees[i].EmployeeId = i + 1;
        }
        foreach (var a in assignmentsB)
        {
            a.EmployeeId = a.Employee.EmployeeId;
        }
        foreach (var av in availsB)
        {
            av.EmployeeId = av.Employee.EmployeeId;
        }

        var shiftTypes = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4, IsActive = true },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4, IsActive = true },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4, IsActive = true }
        };

        var valResult = _validationService.ValidateSchedule(
            periodB,
            dataSet.Employees,
            availsB,
            new List<LeaveRequest>(),
            reqsB,
            assignmentsB,
            shiftTypes);

        Assert.True(valResult.IsValid, $"Validation failed with errors: {string.Join("; ", valResult.HardErrors)}");
        Assert.Empty(valResult.HardErrors);
    }

    [Fact(DisplayName = "TC-18: Precondition A - 0 employee + 0 period => ProceedWithFreshSeed")]
    public void TC18_Precondition_A_ZeroEmployee_ZeroPeriod_Proceeds()
    {
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 0,
            existingEmployeeCodes: Array.Empty<string>(),
            existingPeriodNames: Array.Empty<string>());

        Assert.Equal(DemoDataSeeder.SeedDecision.ProceedWithFreshSeed, decision);
    }

    [Fact(DisplayName = "TC-19: Precondition B - 0 employee + 1 non-demo business period => SkipExistingBusinessData")]
    public void TC19_Precondition_B_ZeroEmployee_NonDemoBusinessPeriod_SkipsSafely()
    {
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 0,
            existingEmployeeCodes: Array.Empty<string>(),
            existingPeriodNames: new[] { "Kỳ lịch nghiệp vụ 1" });

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipExistingBusinessData, decision);
    }

    [Fact(DisplayName = "TC-20: Precondition C - NV01-NV08 + KHÔNG demo period => SkipExistingBusinessData")]
    public void TC20_Precondition_C_ExistingBusinessDb_WithNV01ToNV08_NoDemoPeriod_SkipsSafely()
    {
        var businessPeriods = new[] { "Kỳ lịch 1", "Kỳ lịch 31/08/2026 - 06/09/2026", "Kỳ lịch tháng 9" };

        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingEmployeeCodes: DemoDataSeeder.DemoEmployeeCodes,
            existingPeriodNames: businessPeriods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipExistingBusinessData, decision);
    }

    [Fact(DisplayName = "TC-21: Precondition - Existing unrelated employees => SkipExistingBusinessData")]
    public void TC21_Precondition_ExistingUnrelatedEmployees_SkipsSafely()
    {
        var businessCodes = new[] { "EMP01", "EMP02", "STAFF01", "STAFF02", "STAFF03" };
        var businessPeriods = new[] { "Kỳ lịch tháng 8", "Kỳ lịch tháng 9" };

        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 5,
            existingEmployeeCodes: businessCodes,
            existingPeriodNames: businessPeriods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipExistingBusinessData, decision);
    }

    [Fact(DisplayName = "TC-22: Precondition - User creates Draft WorkSchedule and assignments on Ready period => SkipsAlreadySeeded")]
    public void TC22_Precondition_CompleteNewDemo_UserModifiesReadyPeriod_SkipsAlreadySeeded()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        // Kể cả khi Ready period đã có WorkSchedule Draft và assignments
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingPeriodNames: periods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decision);
    }

    [Fact(DisplayName = "TC-23: Precondition - User modifies Registration availability => SkipsAlreadySeeded")]
    public void TC23_Precondition_CompleteNewDemo_UserModifiesRegistrationAvailability_SkipsAlreadySeeded()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        // Kể cả khi nhân viên đổi lịch rảnh thành toàn bộ Normal hoặc không có Preferred
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingPeriodNames: periods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decision);
    }

    [Fact(DisplayName = "TC-24: Precondition - Staffing count of Ready/Registration modified => SkipsAlreadySeeded")]
    public void TC24_Precondition_CompleteNewDemo_StaffingChanged_SkipsAlreadySeeded()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        // Kể cả khi định biên nhân sự của kỳ bị thay đổi trong quá trình vận hành
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingPeriodNames: periods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decision);
    }

    [Fact(DisplayName = "TC-25: Precondition - Employees changed or deleted after seed => SkipsAlreadySeeded")]
    public void TC25_Precondition_CompleteNewDemo_EmployeesChangedOrDeleted_SkipsAlreadySeeded()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        // Kể cả khi số lượng nhân viên bị thay đổi (thêm mới hoặc xóa bớt), miễn 3 marker còn đủ
        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 5,
            existingPeriodNames: periods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decision);
    }

    [Fact(DisplayName = "TC-26: Precondition - Legacy demo with modified business data => SkipsLegacyDemoDataset")]
    public void TC26_Precondition_LegacyDemo_BusinessDataModified_SkipsLegacyDemoDataset()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đăng ký)"
        };

        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingPeriodNames: periods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipLegacyDemoDataset, decision);
    }

    [Fact(DisplayName = "TC-26B: Precondition - Full new 3-period demo => SkipAlreadySeeded")]
    public void TC26B_Precondition_FullNew3PeriodDemo_AllConditionsMet_SkipsAlreadySeeded()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        var decision = DemoDataSeeder.EvaluateSeedPrecondition(
            totalEmployees: 8,
            existingPeriodNames: periods);

        Assert.Equal(DemoDataSeeder.SeedDecision.SkipAlreadySeeded, decision);
    }

    [Fact(DisplayName = "TC-27: Precondition - Only Demo registration period => Partial demo => Throw InvalidOperationException")]
    public void TC27_Precondition_OnlyDemoRegistrationPeriod_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemoDataSeeder.EvaluateSeedPrecondition(
                totalEmployees: 0,
                existingPeriodNames: new[] { "Kỳ lịch 14-09-2026 (Demo đang đăng ký)" }));

        Assert.StartsWith("Partial demo dataset detected", ex.Message);
    }

    [Fact(DisplayName = "TC-28: Precondition - Only Published demo period => Partial demo => Throw InvalidOperationException")]
    public void TC28_Precondition_OnlyPublishedDemoPeriod_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemoDataSeeder.EvaluateSeedPrecondition(
                totalEmployees: 8,
                existingPeriodNames: new[] { "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)" }));

        Assert.StartsWith("Partial demo dataset detected", ex.Message);
    }

    [Fact(DisplayName = "TC-29: Precondition - Published + Ready missing Registration => Throw InvalidOperationException")]
    public void TC29_Precondition_PublishedAndReady_MissingRegistration_Throws()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)"
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemoDataSeeder.EvaluateSeedPrecondition(
                totalEmployees: 8,
                existingPeriodNames: periods));

        Assert.StartsWith("Partial demo dataset detected", ex.Message);
    }

    [Fact(DisplayName = "TC-29B: Precondition - Published + Registration missing Ready => Throw InvalidOperationException")]
    public void TC29B_Precondition_PublishedAndRegistration_MissingReady_Throws()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đang đăng ký)"
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemoDataSeeder.EvaluateSeedPrecondition(
                totalEmployees: 8,
                existingPeriodNames: periods));

        Assert.StartsWith("Partial demo dataset detected", ex.Message);
    }

    [Fact(DisplayName = "TC-29C: Precondition - Mixing LegacyRegistration with new Ready marker => Throw InvalidOperationException")]
    public void TC29C_Precondition_MixingLegacyRegistrationWithNewMarkers_Throws()
    {
        var periods = new[]
        {
            "Kỳ lịch 31-08-2026 - 06-09-2026 (Demo đã công bố)",
            "Kỳ lịch 07-09-2026 - 13-09-2026 (Demo sẵn sàng xếp lịch)",
            "Kỳ lịch 14-09-2026 - 20-09-2026 (Demo đăng ký)" // Trộn legacy với ready mới
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            DemoDataSeeder.EvaluateSeedPrecondition(
                totalEmployees: 8,
                existingPeriodNames: periods));

        Assert.StartsWith("Partial demo dataset detected", ex.Message);
    }

    [Theory(DisplayName = "TC-30: CalculatePeriodDates tính đúng 3 mốc Thứ Hai cho mọi ngày trong tuần")]
    [InlineData(2026, 9, 7, 2026, 8, 31, 2026, 9, 14, 2026, 9, 21)]  // Monday
    [InlineData(2026, 9, 8, 2026, 8, 31, 2026, 9, 14, 2026, 9, 21)]  // Tuesday
    [InlineData(2026, 9, 11, 2026, 8, 31, 2026, 9, 14, 2026, 9, 21)] // Friday
    [InlineData(2026, 9, 12, 2026, 8, 31, 2026, 9, 14, 2026, 9, 21)] // Saturday
    [InlineData(2026, 9, 13, 2026, 8, 31, 2026, 9, 14, 2026, 9, 21)] // Sunday
    [InlineData(2026, 9, 14, 2026, 9, 7, 2026, 9, 21, 2026, 9, 28)]  // Next Monday
    public void TC30_CalculatePeriodDates_AlwaysReturnsThreeMondays(
        int todayY, int todayM, int todayD,
        int pastY, int pastM, int pastD,
        int readyY, int readyM, int readyD,
        int futureY, int futureM, int futureD)
    {
        var today = new DateOnly(todayY, todayM, todayD);
        var (past, ready, future) = DemoDataSeeder.CalculatePeriodDates(today);

        Assert.Equal(DayOfWeek.Monday, past.DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, ready.DayOfWeek);
        Assert.Equal(DayOfWeek.Monday, future.DayOfWeek);

        Assert.Equal(new DateOnly(pastY, pastM, pastD), past);
        Assert.Equal(new DateOnly(readyY, readyM, readyD), ready);
        Assert.Equal(new DateOnly(futureY, futureM, futureD), future);

        Assert.Equal(14, ready.DayNumber - past.DayNumber);
        Assert.Equal(7, future.DayNumber - ready.DayNumber);
    }
}
