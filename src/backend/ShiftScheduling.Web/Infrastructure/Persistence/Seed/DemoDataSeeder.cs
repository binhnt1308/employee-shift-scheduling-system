using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public sealed class DemoDataSet
{
    public List<UserAccount> Accounts { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
    public List<SchedulePeriod> Periods { get; set; } = new();
    public List<StaffingRequirement> StaffingRequirements { get; set; } = new();
    public List<Availability> Availabilities { get; set; } = new();
    public List<WeeklyAvailabilityPattern> WeeklyPatterns { get; set; } = new();
    public List<WorkSchedule> WorkSchedules { get; set; } = new();
    public List<ShiftAssignment> ShiftAssignments { get; set; } = new();
}

public static class DemoDataSeeder
{
    public const string DefaultDemoPassword = "Password123!";
    public const string PrimaryManagerUsername = "quanly";
    public const string PrimaryEmployeeCode = "NV02";
    public const string PrimaryEmployeeUsername = "nv02";
    public const string PrimaryEmployeeFullName = "Chiêm Trung Hậu";
    public const string PrimaryEmployeeEmail = "2351050042hau@ou.edu.vn";

    public static readonly string[] DemoEmployeeCodes =
    [
        "NV01", "NV02", "NV03", "NV04", "NV05", "NV06", "NV07", "NV08"
    ];

    public const string RegistrationDemoPeriodMarker = "(Demo đăng ký";
    public const string PublishedDemoPeriodMarker = "(Demo đã công bố";

    public static readonly string[] DemoAccountUsernames =
    [
        "admin", "quanly", "nv01", "nv02", "nv03", "nv04", "nv05", "nv06", "nv07", "nv08"
    ];

    /// <summary>
    /// Tính toán các mốc ngày bắt đầu tuần (Thứ Hai) một cách tương đối theo thời gian hiện tại.
    /// Scenario B (quá khứ, đã kết thúc): Thứ Hai tuần trước.
    /// Scenario A (tương lai, đang mở đăng ký): Thứ Hai tuần tới (hoặc tuần kế tiếp nếu hôm nay gần cuối tuần).
    /// </summary>
    public static (DateOnly PastWeekStart, DateOnly FutureWeekStart) CalculatePeriodDates(DateOnly today)
    {
        int daysFromMonday = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
        var currentMonday = today.AddDays(-daysFromMonday);
        var pastWeekStart = currentMonday.AddDays(-7);

        var futureWeekStart = currentMonday.AddDays(7);
        if (today >= futureWeekStart.AddDays(-2))
        {
            futureWeekStart = futureWeekStart.AddDays(7);
        }

        return (pastWeekStart, futureWeekStart);
    }

    /// <summary>
    /// Quyết định thực hiện seed dữ liệu demo dựa trên trạng thái của cơ sở dữ liệu.
    /// </summary>
    public enum SeedDecision
    {
        ProceedWithFreshSeed,
        SkipAlreadySeeded,
        SkipExistingBusinessData
    }

    /// <summary>
    /// Đánh giá tiền điều kiện để quyết định có chạy seed demo hay không:
    /// - Nhánh A (Không có bất kỳ demo period marker nào):
    ///   + totalEmployees == 0 && existingPeriodNames.Count == 0 => ProceedWithFreshSeed
    ///   + totalEmployees > 0 || existingPeriodNames.Count > 0 => SkipExistingBusinessData (DB nghiệp vụ thực tế)
    /// - Nhánh B (Có ít nhất một demo period marker):
    ///   + Đầy đủ NV01..NV08, 2 kỳ demo, Staffing=21, Availabilities>0 (Normal+Preferred), Published Schedule và 42 assignments => SkipAlreadySeeded
    ///   + Thiếu bất kỳ thành phần nào => Ném InvalidOperationException (Partial demo dataset detected)
    /// </summary>
    public static SeedDecision EvaluateSeedPrecondition(
        int totalEmployees,
        IEnumerable<string> existingEmployeeCodes,
        IEnumerable<string> existingPeriodNames,
        int registrationStaffingCount = 21,
        int registrationAvailabilityCount = 1,
        bool hasNormalAvailability = true,
        bool hasPreferredAvailability = true,
        bool hasPublishedWorkSchedule = true,
        int publishedAssignmentCount = 42)
    {
        var periodList = existingPeriodNames.ToList();
        var hasAnyDemoPeriod = periodList.Any(name => name.Contains("(Demo", StringComparison.OrdinalIgnoreCase));

        // Nhánh A: KHÔNG có bất kỳ demo period marker nào
        if (!hasAnyDemoPeriod)
        {
            if (totalEmployees == 0 && periodList.Count == 0)
            {
                return SeedDecision.ProceedWithFreshSeed;
            }

            // Dù có NV01-NV08 hay nhân viên nghiệp vụ khác, hoặc đã có SchedulePeriod nghiệp vụ,
            // khi KHÔNG có demo period marker, đây là dữ liệu nghiệp vụ của cơ sở dữ liệu cũ/thực tế -> Skip an toàn, không throw.
            return SeedDecision.SkipExistingBusinessData;
        }

        // Nhánh B: CÓ ít nhất một demo period marker -> Đây là dataset do DemoDataSeeder quản lý
        var codeSet = new HashSet<string>(existingEmployeeCodes, StringComparer.OrdinalIgnoreCase);
        var hasAllDemoEmployees = DemoEmployeeCodes.All(code => codeSet.Contains(code));
        var hasRegistrationPeriod = periodList.Any(name => name.Contains(RegistrationDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));
        var hasPublishedPeriod = periodList.Any(name => name.Contains(PublishedDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));

        var isCompleteDemo = hasAllDemoEmployees
            && hasRegistrationPeriod
            && hasPublishedPeriod
            && registrationStaffingCount == 21
            && registrationAvailabilityCount > 0
            && hasNormalAvailability
            && hasPreferredAvailability
            && hasPublishedWorkSchedule
            && publishedAssignmentCount == 42;

        if (isCompleteDemo)
        {
            return SeedDecision.SkipAlreadySeeded;
        }

        // Thiếu bất kỳ thành phần nào trong bộ demo hoàn chỉnh -> Fail-fast
        var matchedCodesCount = DemoEmployeeCodes.Count(code => codeSet.Contains(code));
        throw new InvalidOperationException(
            $"Partial demo dataset detected: Database contains demo period markers but lacks a complete demo dataset. " +
            $"Demo employees matched: {matchedCodesCount}/{DemoEmployeeCodes.Length}, " +
            $"Registration demo period: {hasRegistrationPeriod}, " +
            $"Published demo period: {hasPublishedPeriod}, " +
            $"Registration staffing count: {registrationStaffingCount}/21, " +
            $"Registration availability count: {registrationAvailabilityCount}, " +
            $"Registration has Normal: {hasNormalAvailability}, " +
            $"Registration has Preferred: {hasPreferredAvailability}, " +
            $"Published work schedule: {hasPublishedWorkSchedule}, " +
            $"Published assignments count: {publishedAssignmentCount}/42. " +
            "Seeder will not proceed to prevent data corruption.");
    }

    /// <summary>
    /// Kiểm tra xem cơ sở dữ liệu đã có dữ liệu demo hoàn chỉnh hay chưa để bảo đảm tính idempotent.
    /// Trả về true nếu là complete demo dataset, false nếu là fresh hoặc existing business DB.
    /// Sẽ ném InvalidOperationException nếu phát hiện partial demo dataset.
    /// </summary>
    public static bool IsAlreadySeeded(
        IEnumerable<string> existingEmployeeCodes,
        IEnumerable<string> existingPeriodNames,
        int registrationStaffingCount = 21,
        int registrationAvailabilityCount = 1,
        bool hasNormalAvailability = true,
        bool hasPreferredAvailability = true,
        bool hasPublishedWorkSchedule = true,
        int publishedAssignmentCount = 42)
    {
        var codeList = existingEmployeeCodes.ToList();
        var periodList = existingPeriodNames.ToList();
        var decision = EvaluateSeedPrecondition(
            codeList.Count,
            codeList,
            periodList,
            registrationStaffingCount,
            registrationAvailabilityCount,
            hasNormalAvailability,
            hasPreferredAvailability,
            hasPublishedWorkSchedule,
            publishedAssignmentCount);
        return decision == SeedDecision.SkipAlreadySeeded;
    }

    /// <summary>
    /// Xây dựng toàn bộ đối tượng dữ liệu demo trong bộ nhớ, phục vụ cả runtime seed và unit test.
    /// </summary>
    public static DemoDataSet BuildDemoDataSet(
        DateOnly today,
        IPasswordHashService passwordHashService,
        IClock clock)
    {
        var dataSet = new DemoDataSet();
        var (pastWeekStart, futureWeekStart) = CalculatePeriodDates(today);
        var nowUtc = clock.UtcNow;

        // 1. UserAccounts demo: admin, quanly và nv01..nv08
        var passwordHash = passwordHashService.HashPassword(DefaultDemoPassword);

        var adminAccount = new UserAccount
        {
            Username = "admin",
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            IsLocked = false,
            CreatedAt = nowUtc,
            CreatedByAccountId = null
        };

        var managerAccount = new UserAccount
        {
            Username = PrimaryManagerUsername,
            PasswordHash = passwordHash,
            Role = UserRole.Manager,
            IsLocked = false,
            CreatedAt = nowUtc,
            CreatedByAccountId = 1
        };

        var accountNv01 = new UserAccount { Username = "nv01", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };
        var accountNv02 = new UserAccount { Username = "nv02", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };
        var accountNv03 = new UserAccount { Username = "nv03", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };
        var accountNv04 = new UserAccount { Username = "nv04", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };
        var accountNv05 = new UserAccount { Username = "nv05", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };
        var accountNv06 = new UserAccount { Username = "nv06", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };
        var accountNv07 = new UserAccount { Username = "nv07", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };
        var accountNv08 = new UserAccount { Username = "nv08", PasswordHash = passwordHash, Role = UserRole.Employee, IsLocked = false, CreatedAt = nowUtc, CreatedByAccountId = 2 };

        dataSet.Accounts.AddRange(new[]
        {
            adminAccount, managerAccount,
            accountNv01, accountNv02, accountNv03, accountNv04,
            accountNv05, accountNv06, accountNv07, accountNv08
        });

        // 2. Employees quen thuộc: 8 nhân viên (4 Sales [3 FT, 1 PT], 4 Cashier [2 FT, 2 PT])
        var emp01 = new Employee
        {
            EmployeeCode = "NV01",
            FullName = "Nguyễn Văn B",
            DateOfBirth = new DateOnly(1996, 5, 15),
            PhoneNumber = "0901000001",
            Email = "nhanvien@example.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level2,
            StartDate = new DateOnly(2026, 8, 1),
            IsActive = true,
            UserAccount = accountNv01
        };

        var emp02 = new Employee
        {
            EmployeeCode = PrimaryEmployeeCode,
            FullName = PrimaryEmployeeFullName,
            DateOfBirth = new DateOnly(1998, 3, 20),
            PhoneNumber = "0901000002",
            Email = PrimaryEmployeeEmail,
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level2,
            StartDate = new DateOnly(2026, 8, 1),
            IsActive = true,
            UserAccount = accountNv02
        };

        var emp03 = new Employee
        {
            EmployeeCode = "NV03",
            FullName = "NGUYỄN THANH BÌNH",
            DateOfBirth = new DateOnly(1995, 8, 10),
            PhoneNumber = "0901000003",
            Email = "nv03@gmail.com",
            Position = EmployeePosition.Cashier,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level3,
            StartDate = new DateOnly(2026, 8, 25),
            IsActive = true,
            UserAccount = accountNv03
        };

        var emp04 = new Employee
        {
            EmployeeCode = "NV04",
            FullName = "NGUYỄN THANH HÀ",
            DateOfBirth = new DateOnly(1997, 11, 12),
            PhoneNumber = "0901000004",
            Email = "nv04@gmail.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level2,
            StartDate = new DateOnly(2026, 8, 25),
            IsActive = true,
            UserAccount = accountNv04
        };

        var emp05 = new Employee
        {
            EmployeeCode = "NV05",
            FullName = "NGUYỄN THANH HÙNG",
            DateOfBirth = new DateOnly(2001, 2, 14),
            PhoneNumber = "0901000005",
            Email = "nv05@gmail.com",
            Position = EmployeePosition.Cashier,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level2,
            StartDate = new DateOnly(2026, 8, 25),
            IsActive = true,
            UserAccount = accountNv05
        };

        var emp06 = new Employee
        {
            EmployeeCode = "NV06",
            FullName = "NGUYỄN THANH HIỀN",
            DateOfBirth = new DateOnly(2003, 6, 18),
            PhoneNumber = "0901000006",
            Email = "nv06@gmail.com",
            Position = EmployeePosition.Cashier,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level1,
            StartDate = new DateOnly(2026, 8, 25),
            IsActive = true,
            UserAccount = accountNv06
        };

        var emp07 = new Employee
        {
            EmployeeCode = "NV07",
            FullName = "NGUYỄN THỊ C",
            DateOfBirth = new DateOnly(1996, 9, 22),
            PhoneNumber = "0901000007",
            Email = "nv07@gmail.com",
            Position = EmployeePosition.Cashier,
            EmploymentType = EmploymentType.FullTime,
            Level = EmployeeLevel.Level3,
            StartDate = new DateOnly(2026, 8, 25),
            IsActive = true,
            UserAccount = accountNv07
        };

        var emp08 = new Employee
        {
            EmployeeCode = "NV08",
            FullName = "NGUYỄN THỊ D",
            DateOfBirth = new DateOnly(2002, 12, 5),
            PhoneNumber = "0901000008",
            Email = "nv08@gmail.com",
            Position = EmployeePosition.Sales,
            EmploymentType = EmploymentType.PartTime,
            Level = EmployeeLevel.Level1,
            StartDate = new DateOnly(2026, 8, 25),
            IsActive = true,
            UserAccount = accountNv08
        };

        var allEmployees = new List<Employee> { emp01, emp02, emp03, emp04, emp05, emp06, emp07, emp08 };
        dataSet.Employees.AddRange(allEmployees);

        // 3. Weekly Availability Pattern cho NV02 (Chiêm Trung Hậu - NC-02)
        for (int dow = 1; dow <= 6; dow++)
        {
            dataSet.WeeklyPatterns.Add(new WeeklyAvailabilityPattern
            {
                Employee = emp02,
                DayOfWeek = dow,
                ShiftCode = "EVENING",
                PreferenceLevel = AvailabilityPreferenceLevel.Preferred,
                CreatedAt = nowUtc
            });
        }

        // 4. Scenario B - Published Period (Tuần trước, đã kết thúc)
        var pastWeekEnd = pastWeekStart.AddDays(6);
        var pastDeadlineUtc = clock.ToUtcFromVietnamTime(pastWeekStart.AddDays(-1).ToDateTime(new TimeOnly(23, 59, 59)));

        var periodB = new SchedulePeriod
        {
            PeriodName = $"Kỳ lịch {pastWeekStart:dd/MM/yyyy} - {pastWeekEnd:dd/MM/yyyy} (Demo đã công bố)",
            WeekStartDate = pastWeekStart,
            WeekEndDate = pastWeekEnd,
            AvailabilityDeadline = pastDeadlineUtc,
            RegistrationStatus = RegistrationStatus.Closed,
            CreatedByAccountId = 2,
            CreatedAt = nowUtc.AddDays(-14)
        };
        dataSet.Periods.Add(periodB);

        // 21 StaffingRequirements cho Period B (7 ngày x 3 ca: 1 Sales, 1 Cashier, >= 1 Level 2+)
        for (int d = 0; d < 7; d++)
        {
            var date = pastWeekStart.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                dataSet.StaffingRequirements.Add(new StaffingRequirement
                {
                    SchedulePeriod = periodB,
                    WorkDate = date,
                    ShiftTypeId = s,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }

        // Phân công và Availabilities cho Period B (42 ca)
        var periodBAssignments = BuildScenarioBAssignments(
            pastWeekStart, emp01, emp02, emp03, emp04, emp05, emp06, emp07, emp08, nowUtc);

        foreach (var assign in periodBAssignments)
        {
            var isPreferred = (assign.Employee == emp02 && assign.ShiftTypeId == 3)
                              || (assign.Employee == emp03 && assign.ShiftTypeId == 3);

            dataSet.Availabilities.Add(new Availability
            {
                Employee = assign.Employee,
                SchedulePeriod = periodB,
                WorkDate = assign.WorkDate,
                ShiftTypeId = assign.ShiftTypeId,
                PreferenceLevel = isPreferred ? AvailabilityPreferenceLevel.Preferred : AvailabilityPreferenceLevel.Normal,
                RegisteredAt = pastDeadlineUtc.AddDays(-2)
            });
        }

        // WorkSchedule cho Period B (Published)
        var workScheduleB = new WorkSchedule
        {
            SchedulePeriod = periodB,
            ScheduleName = $"Lịch làm việc chính thức tuần {pastWeekStart:dd/MM}",
            Status = ScheduleStatus.Published,
            IsGeneratedByORTools = true,
            CreatedByAccountId = 2,
            PublishedByAccountId = 2,
            CreatedAt = pastDeadlineUtc.AddHours(2),
            PublishedAt = pastWeekStart.AddDays(-1).ToDateTime(new TimeOnly(18, 0)),
            UpdatedAt = nowUtc
        };
        dataSet.WorkSchedules.Add(workScheduleB);

        foreach (var assign in periodBAssignments)
        {
            assign.WorkSchedule = workScheduleB;
        }
        dataSet.ShiftAssignments.AddRange(periodBAssignments);

        // 5. Scenario A - Registration Period (Tuần tới, đang mở đăng ký)
        var futureWeekEnd = futureWeekStart.AddDays(6);
        var futureDeadlineUtc = clock.ToUtcFromVietnamTime(futureWeekStart.AddDays(-1).ToDateTime(new TimeOnly(23, 59, 59)));

        var periodA = new SchedulePeriod
        {
            PeriodName = $"Kỳ lịch {futureWeekStart:dd/MM/yyyy} - {futureWeekEnd:dd/MM/yyyy} (Demo đăng ký)",
            WeekStartDate = futureWeekStart,
            WeekEndDate = futureWeekEnd,
            AvailabilityDeadline = futureDeadlineUtc,
            RegistrationStatus = RegistrationStatus.Open,
            CreatedByAccountId = 2,
            CreatedAt = nowUtc
        };
        dataSet.Periods.Add(periodA);

        // 21 StaffingRequirements cho Period A
        for (int d = 0; d < 7; d++)
        {
            var date = futureWeekStart.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                dataSet.StaffingRequirements.Add(new StaffingRequirement
                {
                    SchedulePeriod = periodA,
                    WorkDate = date,
                    ShiftTypeId = s,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }

        // Availabilities phong phú cho Period A (Normal và Preferred, phủ kín 21 ca và khả thi cho OR-Tools)
        SeedPeriodAAvailabilities(dataSet, periodA, futureWeekStart, allEmployees, nowUtc);

        return dataSet;
    }

    /// <summary>
    /// Thực hiện seed dữ liệu demo vào cơ sở dữ liệu nếu chưa tồn tại.
    /// Đảm bảo idempotent và an toàn foreign key.
    /// </summary>
    public static async Task<bool> SeedAsync(
        ApplicationDbContext dbContext,
        IPasswordHashService passwordHashService,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra an toàn trước khi seed (Safety guard cho database cũ & kiểm tra idempotent & partial demo)
        var totalEmployees = await dbContext.Employees.CountAsync(cancellationToken);

        var existingPeriods = await dbContext.SchedulePeriods
            .Select(p => new { p.SchedulePeriodId, p.PeriodName })
            .ToListAsync(cancellationToken);

        var allPeriodNames = existingPeriods.Select(p => p.PeriodName).ToList();

        var existingDemoCodes = await dbContext.Employees
            .Where(e => DemoEmployeeCodes.Contains(e.EmployeeCode))
            .Select(e => e.EmployeeCode)
            .ToListAsync(cancellationToken);

        int registrationStaffingCount = 0;
        int registrationAvailabilityCount = 0;
        bool hasNormalAvailability = false;
        bool hasPreferredAvailability = false;
        bool hasPublishedWorkSchedule = false;
        int publishedAssignmentCount = 0;

        var registrationDemoPeriod = existingPeriods.FirstOrDefault(
            p => p.PeriodName.Contains(RegistrationDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));

        if (registrationDemoPeriod != null)
        {
            registrationStaffingCount = await dbContext.StaffingRequirements
                .CountAsync(sr => sr.SchedulePeriodId == registrationDemoPeriod.SchedulePeriodId, cancellationToken);

            var registrationAvails = await dbContext.Availabilities
                .Where(a => a.SchedulePeriodId == registrationDemoPeriod.SchedulePeriodId)
                .Select(a => a.PreferenceLevel)
                .ToListAsync(cancellationToken);

            registrationAvailabilityCount = registrationAvails.Count;
            hasNormalAvailability = registrationAvails.Any(l => l == AvailabilityPreferenceLevel.Normal);
            hasPreferredAvailability = registrationAvails.Any(l => l == AvailabilityPreferenceLevel.Preferred);
        }

        var publishedDemoPeriod = existingPeriods.FirstOrDefault(
            p => p.PeriodName.Contains(PublishedDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));

        if (publishedDemoPeriod != null)
        {
            var publishedSchedule = await dbContext.WorkSchedules
                .FirstOrDefaultAsync(ws => ws.SchedulePeriodId == publishedDemoPeriod.SchedulePeriodId
                                        && ws.Status == ScheduleStatus.Published, cancellationToken);

            if (publishedSchedule != null)
            {
                hasPublishedWorkSchedule = true;
                publishedAssignmentCount = await dbContext.ShiftAssignments
                    .CountAsync(sa => sa.WorkScheduleId == publishedSchedule.WorkScheduleId, cancellationToken);
            }
        }

        var decision = EvaluateSeedPrecondition(
            totalEmployees,
            existingDemoCodes,
            allPeriodNames,
            registrationStaffingCount,
            registrationAvailabilityCount,
            hasNormalAvailability,
            hasPreferredAvailability,
            hasPublishedWorkSchedule,
            publishedAssignmentCount);

        if (decision != SeedDecision.ProceedWithFreshSeed)
        {
            return false;
        }

        var today = clock.TodayInVietnam;
        var (pastWeekStart, futureWeekStart) = CalculatePeriodDates(today);

        // Tránh xung đột ngày bắt đầu kỳ nếu người dùng đã tạo thủ công kỳ trùng ngày
        if (await dbContext.SchedulePeriods.AnyAsync(
            p => p.WeekStartDate == pastWeekStart || p.WeekStartDate == futureWeekStart, cancellationToken))
        {
            return false;
        }

        var isRelational = dbContext.Database.IsRelational();
        await using var transaction = isRelational
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var dataSet = BuildDemoDataSet(today, passwordHashService, clock);

            // 2. Tra cứu tài khoản hiện có trong DB để tránh trùng username (ví dụ admin, quanly từ ModelSeed)
            var existingAccounts = await dbContext.UserAccounts.ToListAsync(cancellationToken);
            var existingUsernames = existingAccounts
                .Select(u => u.Username)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Liên kết employee với tài khoản hiện có nếu tài khoản đó đã tồn tại
            foreach (var emp in dataSet.Employees)
            {
                if (emp.UserAccount != null)
                {
                    var existingAcc = existingAccounts.FirstOrDefault(
                        u => string.Equals(u.Username, emp.UserAccount.Username, StringComparison.OrdinalIgnoreCase));
                    if (existingAcc != null)
                    {
                        emp.UserAccount = existingAcc;
                        emp.UserAccountId = existingAcc.UserAccountId;
                    }
                }
            }

            // Chỉ thêm mới các tài khoản chưa có trong DB
            var accountsToInsert = dataSet.Accounts
                .Where(a => !existingUsernames.Contains(a.Username))
                .ToList();

            if (accountsToInsert.Count > 0)
            {
                await dbContext.UserAccounts.AddRangeAsync(accountsToInsert, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Cập nhật UserAccountId cho nhân viên sau khi account đã có khóa chính
            foreach (var emp in dataSet.Employees)
            {
                if (emp.UserAccount != null && emp.UserAccount.UserAccountId > 0)
                {
                    emp.UserAccountId = emp.UserAccount.UserAccountId;
                }
            }

            // 3. Tra cứu ca làm việc thực tế trong DB để map ShiftTypeId đúng với phiên bản hiện hành
            var activeShiftTypes = await dbContext.ShiftTypes
                .Where(st => st.IsActive)
                .OrderBy(st => st.DisplayOrder)
                .ToListAsync(cancellationToken);

            if (activeShiftTypes.Count >= 3)
            {
                var morningShift = activeShiftTypes.FirstOrDefault(st => st.ShiftCode == "MORNING") ?? activeShiftTypes[0];
                var afternoonShift = activeShiftTypes.FirstOrDefault(st => st.ShiftCode == "AFTERNOON") ?? activeShiftTypes[1];
                var eveningShift = activeShiftTypes.FirstOrDefault(st => st.ShiftCode == "EVENING") ?? activeShiftTypes[2];

                var shiftIdMap = new Dictionary<int, int>
                {
                    { 1, morningShift.ShiftTypeId },
                    { 2, afternoonShift.ShiftTypeId },
                    { 3, eveningShift.ShiftTypeId }
                };

                foreach (var req in dataSet.StaffingRequirements)
                {
                    if (shiftIdMap.TryGetValue(req.ShiftTypeId, out var realId))
                    {
                        req.ShiftTypeId = realId;
                    }
                }

                foreach (var avail in dataSet.Availabilities)
                {
                    if (shiftIdMap.TryGetValue(avail.ShiftTypeId, out var realId))
                    {
                        avail.ShiftTypeId = realId;
                    }
                }

                foreach (var assign in dataSet.ShiftAssignments)
                {
                    if (shiftIdMap.TryGetValue(assign.ShiftTypeId, out var realId))
                    {
                        assign.ShiftTypeId = realId;
                    }
                }
            }

            // 4. Đồng bộ CreatedByAccountId với tài khoản quản lý
            var managerAccount = existingAccounts.FirstOrDefault(u => string.Equals(u.Username, PrimaryManagerUsername, StringComparison.OrdinalIgnoreCase))
                ?? accountsToInsert.FirstOrDefault(u => string.Equals(u.Username, PrimaryManagerUsername, StringComparison.OrdinalIgnoreCase));
            int managerId = managerAccount?.UserAccountId > 0 ? managerAccount.UserAccountId : 2;

            foreach (var period in dataSet.Periods)
            {
                period.CreatedByAccountId = managerId;
            }
            foreach (var ws in dataSet.WorkSchedules)
            {
                ws.CreatedByAccountId = managerId;
                ws.PublishedByAccountId = managerId;
            }

            // 5. Thêm dữ liệu demo vào cơ sở dữ liệu
            await dbContext.Employees.AddRangeAsync(dataSet.Employees, cancellationToken);
            await dbContext.WeeklyAvailabilityPatterns.AddRangeAsync(dataSet.WeeklyPatterns, cancellationToken);
            await dbContext.SchedulePeriods.AddRangeAsync(dataSet.Periods, cancellationToken);
            await dbContext.StaffingRequirements.AddRangeAsync(dataSet.StaffingRequirements, cancellationToken);
            await dbContext.Availabilities.AddRangeAsync(dataSet.Availabilities, cancellationToken);
            await dbContext.WorkSchedules.AddRangeAsync(dataSet.WorkSchedules, cancellationToken);
            await dbContext.ShiftAssignments.AddRangeAsync(dataSet.ShiftAssignments, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    private static List<ShiftAssignment> BuildScenarioBAssignments(
        DateOnly pastWeekStart,
        Employee emp01,
        Employee emp02,
        Employee emp03,
        Employee emp04,
        Employee emp05,
        Employee emp06,
        Employee emp07,
        Employee emp08,
        DateTime nowUtc)
    {
        var assignments = new List<ShiftAssignment>();

        // Sales Matrix (7 ngày x 3 ca = 21 ca):
        // emp01 (Sales FT L2): 6 ca (Mon S1, Tue S1, Wed S1, Thu S1, Fri S1, Sat S1) - Sun nghỉ
        // emp02 (Sales FT L2 - Chiêm Trung Hậu): 6 ca (Mon S3, Tue S3, Wed S2, Thu S3, Fri S3, Sun S1) - Sat nghỉ
        // emp04 (Sales FT L2): 6 ca (Mon S2, Tue S2, Thu S2, Fri S2, Sat S2, Sun S2) - Wed nghỉ
        // emp08 (Sales PT L1): 3 ca (Wed S3, Sat S3, Sun S3)
        var salesPlan = new Employee[,]
        {
            { emp01, emp04, emp02 }, // Day 0 (Mon): S1(emp01), S2(emp04), S3(emp02)
            { emp01, emp04, emp02 }, // Day 1 (Tue): S1(emp01), S2(emp04), S3(emp02)
            { emp01, emp02, emp08 }, // Day 2 (Wed): S1(emp01), S2(emp02), S3(emp08) (emp04 nghỉ)
            { emp01, emp04, emp02 }, // Day 3 (Thu): S1(emp01), S2(emp04), S3(emp02)
            { emp01, emp04, emp02 }, // Day 4 (Fri): S1(emp01), S2(emp04), S3(emp02) -> NC-07 Chiêm Trung Hậu!
            { emp01, emp04, emp08 }, // Day 5 (Sat): S1(emp01), S2(emp04), S3(emp08) (emp02 nghỉ)
            { emp02, emp04, emp08 }  // Day 6 (Sun): S1(emp02), S2(emp04), S3(emp08) (emp01 nghỉ)
        };

        // Cashier Matrix (7 ngày x 3 ca = 21 ca):
        // emp03 (Cashier FT L3 - Bình): 6 ca (Mon S3, Tue S3, Wed S3, Fri S3, Sat S3, Sun S3) - Thu nghỉ
        // emp07 (Cashier FT L3): 6 ca (Mon S2, Tue S2, Wed S2, Thu S2, Fri S2, Sun S2) - Sat nghỉ
        // emp05 (Cashier PT L2): 5 ca (Mon S1, Wed S1, Thu S1, Fri S1, Sat S1) - Tue, Sun nghỉ
        // emp06 (Cashier PT L1): 4 ca (Tue S1, Thu S3, Sat S2, Sun S1) - Mon, Wed, Fri nghỉ
        var cashierPlan = new Employee[,]
        {
            { emp05, emp07, emp03 }, // Day 0 (Mon): S1(emp05), S2(emp07), S3(emp03)
            { emp06, emp07, emp03 }, // Day 1 (Tue): S1(emp06), S2(emp07), S3(emp03)
            { emp05, emp07, emp03 }, // Day 2 (Wed): S1(emp05), S2(emp07), S3(emp03)
            { emp05, emp07, emp06 }, // Day 3 (Thu): S1(emp05), S2(emp07), S3(emp06) (emp03 nghỉ)
            { emp05, emp07, emp03 }, // Day 4 (Fri): S1(emp05), S2(emp07), S3(emp03)
            { emp05, emp06, emp03 }, // Day 5 (Sat): S1(emp05), S2(emp06), S3(emp03) (emp07 nghỉ)
            { emp06, emp07, emp03 }  // Day 6 (Sun): S1(emp06), S2(emp07), S3(emp03)
        };

        for (int d = 0; d < 7; d++)
        {
            var date = pastWeekStart.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                // Sales assignment
                var salesEmp = salesPlan[d, s - 1];
                var salesAssign = new ShiftAssignment
                {
                    Employee = salesEmp,
                    WorkDate = date,
                    ShiftTypeId = s,
                    Source = AssignmentSource.ORTools,
                    CreatedAt = nowUtc.AddDays(-10)
                };

                // Demo NC-07: Thứ Sáu (d == 4), Ca tối EVENING (s == 3) của Chiêm Trung Hậu (emp02)
                if (d == 4 && s == 3 && salesEmp == emp02)
                {
                    salesAssign.ActualWorkedHours = 3.50m;
                    salesAssign.WorkdayAdjustmentReason = "về sớm 30 phút theo điều chỉnh của quản lý";
                    salesAssign.UpdatedAt = nowUtc.AddDays(-3);
                }

                assignments.Add(salesAssign);

                // Cashier assignment
                var cashierEmp = cashierPlan[d, s - 1];
                var cashierAssign = new ShiftAssignment
                {
                    Employee = cashierEmp,
                    WorkDate = date,
                    ShiftTypeId = s,
                    Source = AssignmentSource.ORTools,
                    CreatedAt = nowUtc.AddDays(-10)
                };
                assignments.Add(cashierAssign);
            }
        }

        return assignments;
    }

    private static void SeedPeriodAAvailabilities(
        DemoDataSet dataSet,
        SchedulePeriod periodA,
        DateOnly futureWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc)
    {
        var emp01 = allEmployees[0]; // Sales FT L2 (NV01)
        var emp02 = allEmployees[1]; // Sales FT L2 (NV02 - Chiêm Trung Hậu)
        var emp03 = allEmployees[2]; // Cashier FT L3 (NV03 - Bình)
        var emp04 = allEmployees[3]; // Sales FT L2 (NV04)
        var emp05 = allEmployees[4]; // Cashier PT L2 (NV05)
        var emp06 = allEmployees[5]; // Cashier PT L1 (NV06)
        var emp07 = allEmployees[6]; // Cashier FT L3 (NV07)
        var emp08 = allEmployees[7]; // Sales PT L1 (NV08)

        for (int d = 0; d < 7; d++)
        {
            var date = futureWeekStart.AddDays(d);

            // emp01 (Sales FT L2): Rảnh S1 (Preferred), S2 (Normal) các ngày Mon-Sat
            if (d < 6)
            {
                AddAvail(dataSet, emp01, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp01, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            }

            // emp02 (Sales FT L2 - Chiêm Trung Hậu): Rảnh S2 (Preferred), S3 (Preferred), S1 (Normal) các ngày Tue-Sun; Mon S2, S3
            if (d >= 1)
            {
                AddAvail(dataSet, emp02, periodA, date, 2, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp02, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp02, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
            }
            else
            {
                AddAvail(dataSet, emp02, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
                AddAvail(dataSet, emp02, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            }

            // emp04 (Sales FT L2): Rảnh S2 (Normal), S3 (Preferred), S1 (Preferred)
            AddAvail(dataSet, emp04, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            AddAvail(dataSet, emp04, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            if (d == 5 || d == 6)
            {
                AddAvail(dataSet, emp04, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
            }

            // emp08 (Sales PT L1): Rảnh S1 (Normal), S3 (Preferred) các ngày Mon, Wed, Thu, Fri, Sat, Sun
            if (d != 1)
            {
                AddAvail(dataSet, emp08, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
                AddAvail(dataSet, emp08, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            }

            // emp03 (Cashier FT L3 - Bình): Rảnh S2 (Normal), S3 (Preferred), S1 (Normal)
            AddAvail(dataSet, emp03, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            AddAvail(dataSet, emp03, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            if (d < 5)
            {
                AddAvail(dataSet, emp03, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
            }

            // emp07 (Cashier FT L3): Rảnh S1 (Preferred), S2 (Preferred), S3 (Normal)
            AddAvail(dataSet, emp07, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
            AddAvail(dataSet, emp07, periodA, date, 2, AvailabilityPreferenceLevel.Preferred, nowUtc);
            AddAvail(dataSet, emp07, periodA, date, 3, AvailabilityPreferenceLevel.Normal, nowUtc);

            // emp05 (Cashier PT L2): Rảnh S1 (Preferred), S2 (Normal) các ngày Mon-Sat
            if (d < 6)
            {
                AddAvail(dataSet, emp05, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp05, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            }

            // emp06 (Cashier PT L1): Rảnh S2 (Preferred), S3 (Normal), S1 (Normal) các ngày Mon, Tue, Wed, Thu, Fri, Sun
            if (d != 5)
            {
                AddAvail(dataSet, emp06, periodA, date, 2, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp06, periodA, date, 3, AvailabilityPreferenceLevel.Normal, nowUtc);
                AddAvail(dataSet, emp06, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
            }
        }
    }

    private static void AddAvail(
        DemoDataSet dataSet,
        Employee emp,
        SchedulePeriod period,
        DateOnly date,
        int shiftTypeId,
        AvailabilityPreferenceLevel prefLevel,
        DateTime nowUtc)
    {
        dataSet.Availabilities.Add(new Availability
        {
            Employee = emp,
            SchedulePeriod = period,
            WorkDate = date,
            ShiftTypeId = shiftTypeId,
            PreferenceLevel = prefLevel,
            RegisteredAt = nowUtc
        });
    }
}
