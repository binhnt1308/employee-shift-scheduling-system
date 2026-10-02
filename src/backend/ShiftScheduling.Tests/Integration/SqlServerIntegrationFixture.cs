using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Infrastructure.Persistence;
using Xunit;

namespace ShiftScheduling.Tests.Integration;

// Fixture tạo/reset database SQL Server integration và cung cấp clock xác định cho các kiểm thử tích hợp.
[CollectionDefinition("SqlServerIntegration", DisableParallelization = true)]
public class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
}

public sealed class SqlServerIntegrationFixture : IAsyncLifetime
{
    public const string TestDatabaseName = "ShiftSchedulingDb_IntegrationTest";

    private static readonly HashSet<string> ForbiddenDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "ShiftSchedulingDb",
        "ShiftSchedulingDb_Demo",
        "master",
        "tempdb",
        "model",
        "msdb"
    };

    public DbContextOptions<ApplicationDbContext> Options { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public void ValidateSafetyAndInitializeOptions()
    {
        var connStr = Environment.GetEnvironmentVariable("SHIFT_SCHEDULING_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                "Environment variable SHIFT_SCHEDULING_TEST_CONNECTION is not set or empty. " +
                "Integration tests require an explicit SQL Server connection string pointing to " +
                $"'{TestDatabaseName}'.");
        }

        var builder = new SqlConnectionStringBuilder(connStr);
        var database = builder.InitialCatalog?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException(
                "SAFETY VIOLATION: Database name (Initial Catalog) cannot be empty. " +
                $"Integration tests must target '{TestDatabaseName}'.");
        }

        if (ForbiddenDatabases.Contains(database))
        {
            throw new InvalidOperationException(
                $"CRITICAL SAFETY VIOLATION: SHIFT_SCHEDULING_TEST_CONNECTION points to forbidden database '{database}'. " +
                "Integration tests are strictly prohibited from touching this database.");
        }

        if (!string.Equals(database, TestDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SAFETY VIOLATION: Test database must be exactly '{TestDatabaseName}', " +
                $"but connection string specified '{database}'.");
        }

        ConnectionString = connStr;
        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connStr)
            .Options;
    }

    public async Task InitializeAsync()
    {
        ValidateSafetyAndInitializeOptions();

        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await EnsureBaselineDataAsync(db);
    }

    public async Task DisposeAsync()
    {
        ValidateSafetyAndInitializeOptions();

        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    public ApplicationDbContext CreateDbContext()
    {
        if (Options is null)
        {
            ValidateSafetyAndInitializeOptions();
        }

        return new ApplicationDbContext(Options!);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM [AuditLogs];
            DELETE FROM [Notifications];
            DELETE FROM [ShiftTransferRequests];
            DELETE FROM [LeaveRequests];
            DELETE FROM [ShiftAssignments];
            DELETE FROM [WorkSchedules];
            DELETE FROM [WeeklyAvailabilityPatterns];
            DELETE FROM [Availabilities];
            DELETE FROM [StaffingRequirements];
            DELETE FROM [SchedulePeriods];
            DELETE FROM [Employees] WHERE [EmployeeId] <> 1;
            DELETE FROM [UserAccounts] WHERE [UserAccountId] NOT IN (1, 2, 3);
        ");

        await EnsureBaselineDataAsync(db);
    }

    public static async Task EnsureBaselineDataAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM [dbo].[UserAccounts] WHERE [UserAccountId] = 3)
            BEGIN
                SET IDENTITY_INSERT [dbo].[UserAccounts] ON;
                INSERT INTO [dbo].[UserAccounts]
                    ([UserAccountId], [Username], [PasswordHash], [Role], [IsLocked], [CreatedAt], [CreatedByAccountId])
                VALUES
                    (3, 'nhanvien', '100000.o2UkhUfzacTkDzOTs2NgPQ==.H/cGgTusNfv1PTAS9pTbBKEfNKMSw5IurN0GUEwEh90=', 3, 0, '2026-08-01T00:00:00.000Z', 1);
                SET IDENTITY_INSERT [dbo].[UserAccounts] OFF;
            END
            ELSE
            BEGIN
                UPDATE [dbo].[UserAccounts]
                SET [Username] = 'nhanvien',
                    [Role] = 3,
                    [IsLocked] = 0
                WHERE [UserAccountId] = 3;
            END

            IF NOT EXISTS (SELECT 1 FROM [dbo].[Employees] WHERE [EmployeeId] = 1)
            BEGIN
                SET IDENTITY_INSERT [dbo].[Employees] ON;
                INSERT INTO [dbo].[Employees]
                    ([EmployeeId], [UserAccountId], [EmployeeCode], [FullName], [DateOfBirth], [PhoneNumber], [Email], [Position], [EmploymentType], [Level], [StartDate], [IsActive])
                VALUES
                    (1, 3, 'EMP001', N'Nguyễn Văn Test', '2000-01-01', '0901234567', 'nhanvien.test@example.com', 1, 1, 2, '2026-01-01', 1);
                SET IDENTITY_INSERT [dbo].[Employees] OFF;
            END
            ELSE
            BEGIN
                UPDATE [dbo].[Employees]
                SET [UserAccountId] = 3,
                    [EmployeeCode] = 'EMP001',
                    [FullName] = N'Nguyễn Văn Test',
                    [DateOfBirth] = '2000-01-01',
                    [PhoneNumber] = '0901234567',
                    [Email] = 'nhanvien.test@example.com',
                    [Position] = 1,
                    [EmploymentType] = 1,
                    [Level] = 2,
                    [StartDate] = '2026-01-01',
                    [IsActive] = 1
                WHERE [EmployeeId] = 1;
            END
        ");
    }

    public static ControllerContext CreateControllerContext(int accountId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public static ITempDataDictionary CreateTempData(ControllerContext context)
    {
        return new TempDataDictionary(context.HttpContext, new TestTempDataProvider());
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    public sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; }
        public DateOnly TodayInVietnam => DateOnly.FromDateTime(ToVietnamTime(UtcNow));

        public TestClock(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        public DateTime ToVietnamTime(DateTime utcDateTime)
        {
            return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc).AddHours(7);
        }

        public DateTime ToUtcFromVietnamTime(DateTime vietnamDateTime)
        {
            var unspecified = DateTime.SpecifyKind(vietnamDateTime, DateTimeKind.Unspecified);
            return DateTime.SpecifyKind(unspecified.AddHours(-7), DateTimeKind.Utc);
        }
    }
}
