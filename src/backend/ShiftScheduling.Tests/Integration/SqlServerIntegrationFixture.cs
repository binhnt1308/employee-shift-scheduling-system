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

[CollectionDefinition("SqlServerIntegration", DisableParallelization = true)]
public class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
}

public sealed class SqlServerIntegrationFixture : IAsyncLifetime
{
    public const string TestDatabaseName = "ShiftSchedulingDb_IntegrationTest";
    public const string TargetDataSourceSubstring = "MSSQLLocalDB";

    public DbContextOptions<ApplicationDbContext> Options { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public void ValidateSafetyAndInitializeOptions()
    {
        var connStr = Environment.GetEnvironmentVariable("SHIFT_SCHEDULING_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                "Environment variable SHIFT_SCHEDULING_TEST_CONNECTION is not set or empty. " +
                "Integration tests require a dedicated SQL Server LocalDB connection string.");
        }

        var builder = new SqlConnectionStringBuilder(connStr);
        var database = builder.InitialCatalog.Trim();
        var dataSource = builder.DataSource.Trim();

        if (string.Equals(database, "ShiftSchedulingDb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CRITICAL SAFETY VIOLATION: SHIFT_SCHEDULING_TEST_CONNECTION points to production/development " +
                "database 'ShiftSchedulingDb'. Integration tests are strictly prohibited from touching ShiftSchedulingDb.");
        }

        if (!string.Equals(database, TestDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SAFETY VIOLATION: Test database must be exactly '{TestDatabaseName}', but connection string specified '{database}'.");
        }

        if (!dataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) ||
            !dataSource.Contains(TargetDataSourceSubstring, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SAFETY VIOLATION: DataSource must be LocalDB MSSQLLocalDB, but connection string specified '{dataSource}'.");
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
            DELETE FROM [Employees] WHERE [EmployeeId] > 1;
            DELETE FROM [UserAccounts] WHERE [UserAccountId] > 3;
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
