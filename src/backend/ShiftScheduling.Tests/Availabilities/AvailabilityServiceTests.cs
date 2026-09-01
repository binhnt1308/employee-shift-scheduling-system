using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.Availabilities.Services;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.Availabilities;

public sealed class AvailabilityServiceTests
{
    [Fact(DisplayName = "TC04-01 - UC04 BR04 - Kỳ lịch đang mở và chưa hết hạn"
        + " -> Lưu lịch rảnh thành công")]
    public async Task SaveEmployeeAvailabilityAsync_WhenPeriodIsOpenAndDeadlineNotExpired_Succeeds()
    {
        var availRepo = new FakeAvailabilityRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        var empRepo = new FakeEmployeeRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            PeriodName = "Kỳ lịch tuần 33",
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        periodRepo.Add(period);

        var employee = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV001",
            FullName = "Nguyễn Văn A",
            UserAccountId = 10,
            IsActive = true
        };
        empRepo.Add(employee);

        var service = new AvailabilityService(
            availRepo, periodRepo, shiftTypeRepo, empRepo,
            new FakeAuditLogRepository(),
            new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(),
            new FakeClock(new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc)),
            NullLogger<AvailabilityService>.Instance);

        var keys = new List<string> { "2026-08-10_1", "2026-08-10_2" };
        var result = await service.SaveEmployeeAvailabilityAsync(10, 1, keys);

        Assert.True(result.Succeeded);
        Assert.Equal(2, availRepo.Availabilities.Count);
    }

    [Fact(DisplayName = "TC04-02 - UC04 BR04 - Đã hết thời hạn đăng ký (Deadline expired)"
        + " -> Từ chối đăng ký")]
    public async Task SaveEmployeeAvailabilityAsync_WhenDeadlineExpired_ReturnsDeadlineExpiredError()
    {
        var availRepo = new FakeAvailabilityRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        var empRepo = new FakeEmployeeRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16),
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Open
        };
        periodRepo.Add(period);

        var employee = new Employee { EmployeeId = 1, UserAccountId = 10, IsActive = true };
        empRepo.Add(employee);

        var service = new AvailabilityService(
            availRepo, periodRepo, shiftTypeRepo, empRepo,
            new FakeAuditLogRepository(),
            new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(),
            new FakeClock(new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc)), // Sau deadline
            NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(
            10, 1, new List<string> { "2026-08-10_1" });

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.RegistrationDeadlineExpired, result.ErrorCode);
    }

    [Fact(DisplayName = "TC04-03 - UC04 BR04 - Kỳ lịch ở trạng thái Closed -> Từ chối đăng ký")]
    public async Task SaveEmployeeAvailabilityAsync_WhenPeriodClosed_ReturnsInvalidStatusError()
    {
        var availRepo = new FakeAvailabilityRepository();
        var periodRepo = new FakeSchedulePeriodRepository();
        var shiftTypeRepo = CreateShiftTypeRepositoryWithThreeFixedShifts();
        var empRepo = new FakeEmployeeRepository();

        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            AvailabilityDeadline = new DateTime(2026, 8, 8, 23, 59, 0, DateTimeKind.Utc),
            RegistrationStatus = RegistrationStatus.Closed
        };
        periodRepo.Add(period);

        var employee = new Employee { EmployeeId = 1, UserAccountId = 10, IsActive = true };
        empRepo.Add(employee);

        var service = new AvailabilityService(
            availRepo, periodRepo, shiftTypeRepo, empRepo,
            new FakeAuditLogRepository(),
            new FakeWeeklyAvailabilityPatternRepository(),
            new FakeUnitOfWork(),
            new FakeClock(new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc)),
            NullLogger<AvailabilityService>.Instance);

        var result = await service.SaveEmployeeAvailabilityAsync(
            10, 1, new List<string> { "2026-08-10_1" });

        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.InvalidSchedulePeriodStatus, result.ErrorCode);
    }

    private static FakeShiftTypeRepository CreateShiftTypeRepositoryWithThreeFixedShifts()
    {
        var repo = new FakeShiftTypeRepository();
        repo.ShiftTypes.AddRange(new[]
        {
            new ShiftType
            {
                ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0),
                DisplayOrder = 1, IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0),
                DisplayOrder = 2, IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0),
                DisplayOrder = 3, IsActive = true
            }
        });
        return repo;
    }

    private sealed class FakeAvailabilityRepository : IAvailabilityRepository
    {
        public List<Availability> Availabilities { get; } = new();

        public Task<List<Availability>> GetByEmployeeAndPeriodAsync(
            int employeeId, int schedulePeriodId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Availabilities.Where(a =>
                    a.EmployeeId == employeeId && a.SchedulePeriodId == schedulePeriodId).ToList());
        }

        public Task<List<Availability>> GetByPeriodWithDetailsAsync(
            int schedulePeriodId,
            int? employeeIdFilter = null,
            EmployeePosition? positionFilter = null,
            DateOnly? dateFilter = null,
            int? shiftTypeIdFilter = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Availabilities.Where(a => a.SchedulePeriodId == schedulePeriodId).ToList());
        }

        public Task SaveEmployeeAvailabilitiesAsync(
            int employeeId,
            int schedulePeriodId,
            IEnumerable<(DateOnly WorkDate, int ShiftTypeId)> selectedShifts,
            IReadOnlyDictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel> preferenceMap,
            DateTime now,
            CancellationToken cancellationToken = default)
        {
            Availabilities.RemoveAll(a => a.EmployeeId == employeeId && a.SchedulePeriodId == schedulePeriodId);
            foreach (var (date, shiftId) in selectedShifts)
            {
                var pref = preferenceMap.TryGetValue((date, shiftId), out var p) ? p : AvailabilityPreferenceLevel.Normal;
                Availabilities.Add(new Availability
                {
                    EmployeeId = employeeId,
                    SchedulePeriodId = schedulePeriodId,
                    WorkDate = date,
                    ShiftTypeId = shiftId,
                    PreferenceLevel = pref,
                    RegisteredAt = now
                });
            }
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeWeeklyAvailabilityPatternRepository : IWeeklyAvailabilityPatternRepository
    {
        public List<WeeklyAvailabilityPattern> Patterns { get; } = new();

        public Task<List<WeeklyAvailabilityPattern>> GetByEmployeeIdAsync(
            int employeeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Patterns.Where(p => p.EmployeeId == employeeId).ToList());
        }

        public Task UpsertByEmployeeAsync(
            int employeeId,
            IEnumerable<(int DayOfWeek, string ShiftCode, AvailabilityPreferenceLevel PreferenceLevel)> entries,
            DateTime now,
            CancellationToken cancellationToken = default)
        {
            Patterns.RemoveAll(p => p.EmployeeId == employeeId);
            foreach (var entry in entries)
            {
                Patterns.Add(new WeeklyAvailabilityPattern
                {
                    EmployeeId = employeeId,
                    DayOfWeek = entry.DayOfWeek,
                    ShiftCode = entry.ShiftCode,
                    PreferenceLevel = entry.PreferenceLevel,
                    CreatedAt = now
                });
            }
            return Task.CompletedTask;
        }
    }
}
