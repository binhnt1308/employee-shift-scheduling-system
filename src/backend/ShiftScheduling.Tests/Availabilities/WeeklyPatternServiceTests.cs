using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.Availabilities.ViewModels;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;
using Xunit;

namespace ShiftScheduling.Tests.Availabilities;

public sealed class WeeklyPatternServiceTests
{
    private static (
        AvailabilityService Service,
        FakeAvailabilityRepository AvailRepo,
        FakeWeeklyAvailabilityPatternRepository WeeklyRepo,
        FakeSchedulePeriodRepository PeriodRepo,
        FakeEmployeeRepository EmpRepo,
        FakeShiftTypeRepository ShiftRepo
    ) CreateTestSetup(DateTime? utcNow = null)
    {
        var availRepo = new FakeAvailabilityRepository();
        var weeklyRepo = new FakeWeeklyAvailabilityPatternRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var empRepo = new FakeEmployeeRepository();
        var shiftRepo = new FakeShiftTypeRepository();

        shiftRepo.ShiftTypes.AddRange(new[]
        {
            new ShiftType
            {
                ShiftTypeId = 10, ShiftCode = "MORNING", ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0),
                DisplayOrder = 1, IsActive = true, ConfigurationVersion = 1
            },
            new ShiftType
            {
                ShiftTypeId = 20, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0),
                DisplayOrder = 2, IsActive = true, ConfigurationVersion = 1
            },
            new ShiftType
            {
                ShiftTypeId = 30, ShiftCode = "EVENING", ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0),
                DisplayOrder = 3, IsActive = true, ConfigurationVersion = 1
            }
        });

        var clock = new FakeClock(utcNow ?? new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc));

        var service = new AvailabilityService(
            availRepo, periodRepo, shiftRepo, empRepo,
            new FakeAuditLogRepository(),
            weeklyRepo,
            new FakeUnitOfWork(),
            clock,
            NullLogger<AvailabilityService>.Instance);

        return (service, availRepo, weeklyRepo, periodRepo, empRepo, shiftRepo);
    }

    [Fact(DisplayName = "TC-NC02-01 - Lưu mẫu tuần hợp lệ -> Thành công")]
    public async Task SaveWeeklyPatternAsync_ValidEntries_Succeeds()
    {
        var (service, _, weeklyRepo, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, EmployeeCode = "NV001", IsActive = true });

        var entries = new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 1, ShiftCode = "MORNING", PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { DayOfWeek = 5, ShiftCode = "EVENING", PreferenceLevel = AvailabilityPreferenceLevel.Preferred }
        };

        var result = await service.SaveWeeklyPatternAsync(100, entries);

        Assert.True(result.Succeeded);
        Assert.Equal(2, weeklyRepo.Patterns.Count);
        Assert.Contains(weeklyRepo.Patterns, p => p.DayOfWeek == 1 && p.ShiftCode == "MORNING" && p.PreferenceLevel == AvailabilityPreferenceLevel.Normal);
        Assert.Contains(weeklyRepo.Patterns, p => p.DayOfWeek == 5 && p.ShiftCode == "EVENING" && p.PreferenceLevel == AvailabilityPreferenceLevel.Preferred);
    }

    [Fact(DisplayName = "TC-NC02-02 - Cập nhật mẫu tuần: Bỏ 1 ô khỏi mẫu -> Record cũ bị xóa (Replace semantics)")]
    public async Task SaveWeeklyPatternAsync_RemovingEntry_DeletesOldRecord()
    {
        var (service, _, weeklyRepo, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, EmployeeCode = "NV001", IsActive = true });

        // Lưu lần 1 có 2 ca
        await service.SaveWeeklyPatternAsync(100, new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 1, ShiftCode = "MORNING", PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { DayOfWeek = 2, ShiftCode = "AFTERNOON", PreferenceLevel = AvailabilityPreferenceLevel.Preferred }
        });

        Assert.Equal(2, weeklyRepo.Patterns.Count);

        // Lưu lần 2 bỏ ca Thứ Ba
        var result = await service.SaveWeeklyPatternAsync(100, new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 1, ShiftCode = "MORNING", PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        });

        Assert.True(result.Succeeded);
        Assert.Single(weeklyRepo.Patterns);
        Assert.Equal(1, weeklyRepo.Patterns[0].DayOfWeek);
    }

    [Fact(DisplayName = "TC-NC02-03 - Lưu mẫu danh sách rỗng -> Mẫu tuần được xóa sạch")]
    public async Task SaveWeeklyPatternAsync_EmptyEntries_ClearsAllPatterns()
    {
        var (service, _, weeklyRepo, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, EmployeeCode = "NV001", IsActive = true });

        await service.SaveWeeklyPatternAsync(100, new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 1, ShiftCode = "MORNING", PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        });

        var result = await service.SaveWeeklyPatternAsync(100, new List<WeeklyPatternEntryInputViewModel>());

        Assert.True(result.Succeeded);
        Assert.Empty(weeklyRepo.Patterns);
    }

    [Fact(DisplayName = "TC-NC02-04 - Record-level security: Employee A lưu mẫu không làm đổi mẫu của Employee B")]
    public async Task SaveWeeklyPatternAsync_EmployeeASaving_DoesNotAffectEmployeeB()
    {
        var (service, _, weeklyRepo, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, EmployeeCode = "NV001", IsActive = true });
        empRepo.Add(new Employee { EmployeeId = 2, UserAccountId = 200, EmployeeCode = "NV002", IsActive = true });

        // Employee B có mẫu sẵn
        weeklyRepo.Patterns.Add(new WeeklyAvailabilityPattern
        {
            EmployeeId = 2,
            DayOfWeek = 3,
            ShiftCode = "EVENING",
            PreferenceLevel = AvailabilityPreferenceLevel.Preferred
        });

        // Employee A lưu mẫu của mình
        var result = await service.SaveWeeklyPatternAsync(100, new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 1, ShiftCode = "MORNING", PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        });

        Assert.True(result.Succeeded);
        Assert.Equal(2, weeklyRepo.Patterns.Count);

        // Mẫu của B vẫn nguyên vẹn
        var patternB = weeklyRepo.Patterns.FirstOrDefault(p => p.EmployeeId == 2);
        Assert.NotNull(patternB);
        Assert.Equal(3, patternB.DayOfWeek);
        Assert.Equal("EVENING", patternB.ShiftCode);
    }

    [Fact(DisplayName = "TC-NC02-05 - Employee inactive -> Từ chối lưu mẫu tuần")]
    public async Task SaveWeeklyPatternAsync_InactiveEmployee_ReturnsError()
    {
        var (service, _, _, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = false });

        var result = await service.SaveWeeklyPatternAsync(100, new List<WeeklyPatternEntryInputViewModel>());

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.EmployeeNotFound, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-NC02-06 - ShiftCode không hợp lệ -> Từ chối")]
    public async Task SaveWeeklyPatternAsync_InvalidShiftCode_ReturnsError()
    {
        var (service, _, _, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        var entries = new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 1, ShiftCode = "NIGHT", PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        var result = await service.SaveWeeklyPatternAsync(100, entries);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.FixedShiftRequired, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-NC02-07 - DayOfWeek ngoài 1-7 -> Từ chối")]
    public async Task SaveWeeklyPatternAsync_DayOfWeekOutOfRange_ReturnsError()
    {
        var (service, _, _, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        var entries = new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 0, ShiftCode = "MORNING", PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        var result = await service.SaveWeeklyPatternAsync(100, entries);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidEnumValue, result.ErrorCode);
    }

    [Fact(DisplayName = "TC-NC02-08 - PreferenceLevel enum ngoài phạm vi -> Từ chối")]
    public async Task SaveWeeklyPatternAsync_PreferenceLevelOutOfRange_ReturnsError()
    {
        var (service, _, _, _, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        var entries = new List<WeeklyPatternEntryInputViewModel>
        {
            new() { DayOfWeek = 2, ShiftCode = "MORNING", PreferenceLevel = (AvailabilityPreferenceLevel)99 }
        };

        var result = await service.SaveWeeklyPatternAsync(100, entries);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidEnumValue, result.ErrorCode);
    }

    // ── Availability Registration Tests (Strict Validation) ──────────────────────────

    [Fact(DisplayName = "TC-NC02-09 - Preference key thừa (không nằm trong selectedShiftKeys) -> Trả Failure và KHÔNG lưu DB")]
    public async Task SaveEmployeeAvailabilityAsync_UnselectedPreferenceKey_ReturnsFailure()
    {
        var (service, availRepo, _, periodRepo, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var selectedKeys = new List<string> { "2026-08-10_10" };
        var preferences = new Dictionary<string, AvailabilityPreferenceLevel>
        {
            { "2026-08-10_10", AvailabilityPreferenceLevel.Preferred },
            { "2026-08-10_20", AvailabilityPreferenceLevel.Normal } // Ca 20 KHÔNG được chọn
        };

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, selectedKeys, preferences);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftSelection, result.ErrorCode);
        Assert.Empty(availRepo.Availabilities); // Atomicity: Không ghi DB
    }

    [Fact(DisplayName = "TC-NC02-10 - Preference key sai định dạng -> Trả Failure và KHÔNG lưu DB")]
    public async Task SaveEmployeeAvailabilityAsync_InvalidPreferenceKeyFormat_ReturnsFailure()
    {
        var (service, availRepo, _, periodRepo, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var selectedKeys = new List<string> { "2026-08-10_10" };
        var preferences = new Dictionary<string, AvailabilityPreferenceLevel>
        {
            { "INVALID_KEY_FORMAT", AvailabilityPreferenceLevel.Preferred }
        };

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, selectedKeys, preferences);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftSelection, result.ErrorCode);
        Assert.Empty(availRepo.Availabilities);
    }

    [Fact(DisplayName = "TC-NC02-11 - Selected shift key sai định dạng -> Trả Failure")]
    public async Task SaveEmployeeAvailabilityAsync_InvalidSelectedShiftKeyFormat_ReturnsFailure()
    {
        var (service, availRepo, _, periodRepo, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var selectedKeys = new List<string> { "NOT_A_DATE_10" };

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, selectedKeys);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftSelection, result.ErrorCode);
        Assert.Empty(availRepo.Availabilities);
    }

    [Fact(DisplayName = "TC-NC02-12 - Selected shift key trùng lặp -> Trả Failure")]
    public async Task SaveEmployeeAvailabilityAsync_DuplicateSelectedShiftKey_ReturnsFailure()
    {
        var (service, availRepo, _, periodRepo, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var selectedKeys = new List<string> { "2026-08-10_10", "2026-08-10_10" };

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, selectedKeys);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidShiftSelection, result.ErrorCode);
        Assert.Empty(availRepo.Availabilities);
    }

    [Fact(DisplayName = "TC-NC02-13 - Preference enum ngoài phạm vi -> Trả Failure")]
    public async Task SaveEmployeeAvailabilityAsync_InvalidPreferenceEnumValue_ReturnsFailure()
    {
        var (service, availRepo, _, periodRepo, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var selectedKeys = new List<string> { "2026-08-10_10" };
        var preferences = new Dictionary<string, AvailabilityPreferenceLevel>
        {
            { "2026-08-10_10", (AvailabilityPreferenceLevel)99 }
        };

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, selectedKeys, preferences);

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidEnumValue, result.ErrorCode);
        Assert.Empty(availRepo.Availabilities);
    }

    [Fact(DisplayName = "TC-NC02-14 - Thiếu preference cho ca rảnh đã chọn -> Mặc định Normal")]
    public async Task SaveEmployeeAvailabilityAsync_MissingPreferenceForSelectedShift_DefaultsToNormal()
    {
        var (service, availRepo, _, periodRepo, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var selectedKeys = new List<string> { "2026-08-10_10", "2026-08-10_20" };
        var preferences = new Dictionary<string, AvailabilityPreferenceLevel>
        {
            { "2026-08-10_10", AvailabilityPreferenceLevel.Preferred }
            // Không truyền 2026-08-10_20
        };

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, selectedKeys, preferences);

        Assert.True(result.Succeeded);
        Assert.Equal(2, availRepo.Availabilities.Count);
        var morning = availRepo.Availabilities.First(a => a.ShiftTypeId == 10);
        Assert.Equal(AvailabilityPreferenceLevel.Preferred, morning.PreferenceLevel);

        var afternoon = availRepo.Availabilities.First(a => a.ShiftTypeId == 20);
        Assert.Equal(AvailabilityPreferenceLevel.Normal, afternoon.PreferenceLevel);
    }

    [Fact(DisplayName = "TC-NC02-15 - Employee không hoạt động -> Từ chối đăng ký lịch rảnh")]
    public async Task SaveEmployeeAvailabilityAsync_InactiveEmployee_ReturnsFailure()
    {
        var (service, availRepo, _, periodRepo, empRepo, _) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = false });
        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var result = await service.SaveEmployeeAvailabilityAsync(100, 1, new List<string> { "2026-08-10_10" });

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.EmployeeNotFound, result.ErrorCode);
        Assert.Empty(availRepo.Availabilities);
    }

    [Fact(DisplayName = "TC-NC02-16 - Ánh xạ ShiftCode sang ShiftTypeId active phiên bản mới")]
    public async Task GetEmployeeRegisterFormAsync_MapsShiftCodeToActiveVersionShiftTypeId()
    {
        var (service, _, weeklyRepo, periodRepo, empRepo, shiftRepo) = CreateTestSetup();
        empRepo.Add(new Employee { EmployeeId = 1, UserAccountId = 100, IsActive = true });

        weeklyRepo.Patterns.Add(new WeeklyAvailabilityPattern
        {
            EmployeeId = 1,
            DayOfWeek = 1,
            ShiftCode = "MORNING",
            PreferenceLevel = AvailabilityPreferenceLevel.Preferred
        });

        shiftRepo.ShiftTypes.Clear();
        shiftRepo.ShiftTypes.AddRange(new[]
        {
            new ShiftType { ShiftTypeId = 101, ShiftCode = "MORNING", ShiftName = "Ca sáng v2", IsActive = true, ConfigurationVersion = 2, DisplayOrder = 1 },
            new ShiftType { ShiftTypeId = 102, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều v2", IsActive = true, ConfigurationVersion = 2, DisplayOrder = 2 },
            new ShiftType { ShiftTypeId = 103, ShiftCode = "EVENING", ShiftName = "Ca tối v2", IsActive = true, ConfigurationVersion = 2, DisplayOrder = 3 }
        });

        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ v2",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 9, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        });

        var form = await service.GetEmployeeRegisterFormAsync(100, 1);

        Assert.NotNull(form);
        Assert.Single(form.WeeklyPattern.Cells);
        Assert.Equal(101, form.WeeklyPattern.Cells[0].ShiftTypeId);
        Assert.Equal(AvailabilityPreferenceLevel.Preferred, form.WeeklyPattern.Cells[0].PreferenceLevel);
    }

    [Fact(DisplayName = "TC-NC02-17 - Manager Overview trả đúng cả ca Normal và Preferred")]
    public async Task GetManagerOverviewAsync_ReturnsBothNormalAndPreferredEmployees()
    {
        var (service, availRepo, _, periodRepo, empRepo, shiftRepo) = CreateTestSetup();

        var emp1 = new Employee { EmployeeId = 1, UserAccountId = 100, EmployeeCode = "NV001", FullName = "A", Position = EmployeePosition.Sales, IsActive = true };
        var emp2 = new Employee { EmployeeId = 2, UserAccountId = 200, EmployeeCode = "NV002", FullName = "B", Position = EmployeePosition.Cashier, IsActive = true };
        empRepo.Add(emp1);
        empRepo.Add(emp2);

        periodRepo.Add(new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ 1",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        });

        availRepo.Availabilities.Add(new Availability
        {
            AvailabilityId = 1,
            EmployeeId = 1,
            SchedulePeriodId = 1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 10,
            PreferenceLevel = AvailabilityPreferenceLevel.Preferred,
            Employee = emp1,
            ShiftType = shiftRepo.ShiftTypes[0]
        });

        availRepo.Availabilities.Add(new Availability
        {
            AvailabilityId = 2,
            EmployeeId = 2,
            SchedulePeriodId = 1,
            WorkDate = new DateOnly(2026, 8, 10),
            ShiftTypeId = 10,
            PreferenceLevel = AvailabilityPreferenceLevel.Normal,
            Employee = emp2,
            ShiftType = shiftRepo.ShiftTypes[0]
        });

        var overview = await service.GetManagerOverviewAsync(1);

        Assert.NotNull(overview);
        var day = overview.Days.First(d => d.WorkDate == new DateOnly(2026, 8, 10));
        var morningCell = day.Shifts.First(s => s.ShiftTypeId == 10);
        Assert.Equal(2, morningCell.AvailableEmployees.Count);
        Assert.Contains(morningCell.AvailableEmployees, e => e.EmployeeId == 1 && e.PreferenceLevel == AvailabilityPreferenceLevel.Preferred);
        Assert.Contains(morningCell.AvailableEmployees, e => e.EmployeeId == 2 && e.PreferenceLevel == AvailabilityPreferenceLevel.Normal);
    }
}
