using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public enum DemoResetValidationStatus
{
    Valid,
    MissingResetArgument,
    EmptyResetDatabaseName,
    DemoDataDisabled,
    AllowResetDisabled,
    EmptyInitialCatalog,
    DatabaseNameMismatch,
    MissingDemoSuffix
}

public sealed class DemoResetValidationResult
{
    public bool IsValid => Status == DemoResetValidationStatus.Valid;
    public DemoResetValidationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TargetDatabaseName { get; init; }

    public static DemoResetValidationResult Success(string targetDatabaseName) =>
        new()
        {
            Status = DemoResetValidationStatus.Valid,
            TargetDatabaseName = targetDatabaseName
        };

    public static DemoResetValidationResult Failure(DemoResetValidationStatus status, string errorMessage) =>
        new()
        {
            Status = status,
            ErrorMessage = errorMessage
        };
}

public static class DemoDataResetCommand
{
    public const string DemoDatabaseSuffix = "_Demo";
    public const string ResetDemoArgPrefix = "--reset-demo=";

    /// <summary>
    /// Trích xuất tham số --reset-demo=<DATABASE_NAME> từ danh sách tham số dòng lệnh.
    /// </summary>
    public static bool TryExtractResetArgument(IEnumerable<string> args, out string? databaseName)
    {
        databaseName = null;
        var rawArg = args.FirstOrDefault(a => a.StartsWith(ResetDemoArgPrefix, StringComparison.OrdinalIgnoreCase));
        if (rawArg == null)
        {
            return false;
        }

        databaseName = rawArg[ResetDemoArgPrefix.Length..].Trim();
        return true;
    }

    /// <summary>
    /// Lọc bỏ tham số --reset-demo khỏi danh sách args trước khi truyền cho WebApplicationBuilder.
    /// </summary>
    public static string[] FilterCommandLineArgs(string[] args)
    {
        return args
            .Where(a => !a.StartsWith(ResetDemoArgPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Kiểm tra toàn bộ các chốt an toàn bắt buộc trước khi thực hiện thao tác reset có tính hủy dữ liệu.
    /// </summary>
    public static DemoResetValidationResult ValidateResetPreconditions(
        bool isResetRequested,
        string? requestedDatabaseName,
        string? actualInitialCatalog,
        bool isDemoDataEnabled,
        bool isAllowResetEnabled)
    {
        if (!isResetRequested)
        {
            return DemoResetValidationResult.Failure(
                DemoResetValidationStatus.MissingResetArgument,
                "Lệnh reset không được yêu cầu.");
        }

        if (string.IsNullOrWhiteSpace(requestedDatabaseName))
        {
            return DemoResetValidationResult.Failure(
                DemoResetValidationStatus.EmptyResetDatabaseName,
                "Tên database xác nhận trong --reset-demo=<DATABASE_NAME> không được để trống.");
        }

        if (!isDemoDataEnabled)
        {
            return DemoResetValidationResult.Failure(
                DemoResetValidationStatus.DemoDataDisabled,
                "Chế độ Demo Data đang bị tắt (DemoData:Enabled = false). Không cho phép reset.");
        }

        if (!isAllowResetEnabled)
        {
            return DemoResetValidationResult.Failure(
                DemoResetValidationStatus.AllowResetDisabled,
                "Cờ DemoData:AllowReset chưa được bật (yêu cầu true). Vui lòng cấu hình biến môi trường.");
        }

        if (string.IsNullOrWhiteSpace(actualInitialCatalog))
        {
            return DemoResetValidationResult.Failure(
                DemoResetValidationStatus.EmptyInitialCatalog,
                "Tên database trong DefaultConnection (Initial Catalog) không hợp lệ hoặc để trống.");
        }

        if (!string.Equals(requestedDatabaseName, actualInitialCatalog, StringComparison.OrdinalIgnoreCase))
        {
            return DemoResetValidationResult.Failure(
                DemoResetValidationStatus.DatabaseNameMismatch,
                $"Tên database xác nhận '{requestedDatabaseName}' không khớp với thực tế '{actualInitialCatalog}'.");
        }

        if (!actualInitialCatalog.EndsWith(DemoDatabaseSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return DemoResetValidationResult.Failure(
                DemoResetValidationStatus.MissingDemoSuffix,
                $"Database '{actualInitialCatalog}' thiếu hậu tố '{DemoDatabaseSuffix}'. " +
                "Chỉ database demo mới được phép reset.");
        }

        return DemoResetValidationResult.Success(actualInitialCatalog);
    }

    /// <summary>
    /// Thực hiện quy trình reset hủy database cũ, chạy migration và tái tạo dữ liệu mẫu 4 kỳ.
    /// </summary>
    public static async Task ExecuteResetAsync(
        ApplicationDbContext dbContext,
        IPasswordHashService passwordHashService,
        IClock clock,
        ILogger logger,
        string environmentName,
        string serverName,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "DESTRUCTIVE DEMO RESET COMMENCING:\n" +
            "  Environment: {Environment}\n" +
            "  Database Server: {Server}\n" +
            "  Database Name: {Database}",
            environmentName,
            serverName,
            databaseName);

        // Chỉ xóa database sau khi đã xác thực hậu tố _Demo và tên xác nhận khớp database thực tế.
        await dbContext.Database.EnsureDeletedAsync(cancellationToken);

        // Tái tạo toàn bộ bảng và chỉ mục thông qua migration để đảm bảo tính nhất quán của schema
        await dbContext.Database.MigrateAsync(cancellationToken);

        // Tái sinh bộ dữ liệu mẫu 4 kỳ theo mốc thời gian thực tế tại thời điểm reset
        var seeded = await DemoDataSeeder.SeedAsync(dbContext, passwordHashService, clock, cancellationToken);
        if (!seeded)
        {
            throw new InvalidOperationException(
                $"Không thể nạp dữ liệu mẫu sau khi reset database '{databaseName}'.");
        }

        logger.LogInformation(
            "Demo database reset succeeded:\n" +
            "  Database: {Database}\n" +
            "  Seeded Date (VN): {TodayVn:yyyy-MM-dd}\n" +
            "  Status: Database recreated with fresh 4-period demo dataset.",
            databaseName,
            clock.TodayInVietnam);
    }
}
