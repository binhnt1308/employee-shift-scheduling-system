using ShiftScheduling.Common.Security;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    /// <summary>
    /// Đồng bộ mật khẩu cho các tài khoản demo đã tồn tại trước khi seed dữ liệu demo.
    /// </summary>
    public static void SynchronizeExistingDemoAccountPasswords(
        IEnumerable<UserAccount> existingAccounts,
        IPasswordHashService passwordHashService)
    {
        var demoUsernames = DemoAccountUsernames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var account in existingAccounts.Where(a => demoUsernames.Contains(a.Username)))
        {
            account.PasswordHash = passwordHashService.HashPassword(DefaultDemoPassword);
        }
    }

    /// <summary>
    /// Dựng tài khoản và hồ sơ nhân viên demo theo cặp để các luồng đăng nhập,
    /// phân quyền và lịch làm việc dùng cùng một bộ dữ liệu.
    /// </summary>
    private static List<Employee> BuildAccountsAndEmployees(
        DemoDataSet dataSet,
        DateTime nowUtc,
        IPasswordHashService passwordHashService)
    {
        // Danh sách tài khoản người dùng: admin, quanly và nv01 đến nv08
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

        // Danh sách 8 nhân viên (4 Bán hàng [3 toàn thời gian, 1 bán thời gian], 4 Thu ngân [2 toàn thời gian, 2 bán thời gian])
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

        // Lịch đăng ký mẫu hàng tuần cho nhân viên NV02 (Chiêm Trung Hậu)
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

        return allEmployees;
    }
}
