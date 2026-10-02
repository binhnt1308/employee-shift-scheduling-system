using ShiftScheduling.Infrastructure.Persistence.Seed;

namespace ShiftScheduling.Tests.Seed;

public sealed class DemoDataResetCommandTests
{
    [Fact(DisplayName = "TC-SEEDRESET-001: Không có tham số reset -> nhận diện khởi động bình thường")]
    public void TC_SEEDRESET_001_NoResetArg_RecognizedAsNormalStartup()
    {
        var args = new[] { "--urls=http://localhost:5000", "--environment=Development" };
        var isRequested = DemoDataResetCommand.TryExtractResetArgument(args, out var dbName);

        Assert.False(isRequested);
        Assert.Null(dbName);

        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: false,
            requestedDatabaseName: null,
            actualInitialCatalog: "ShiftSchedulingDb_Demo",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.MissingResetArgument, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-002: Đầy đủ điều kiện an toàn và hậu tố _Demo -> cho phép reset")]
    public void TC_SEEDRESET_002_ValidDemoDatabaseAndFlags_PermitsReset()
    {
        var args = new[] { "--reset-demo=ShiftSchedulingDb_Demo" };
        var isRequested = DemoDataResetCommand.TryExtractResetArgument(args, out var dbName);

        Assert.True(isRequested);
        Assert.Equal("ShiftSchedulingDb_Demo", dbName);

        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "ShiftSchedulingDb_Demo",
            actualInitialCatalog: "ShiftSchedulingDb_Demo",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.True(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.Valid, validation.Status);
        Assert.Equal("ShiftSchedulingDb_Demo", validation.TargetDatabaseName);
    }

    [Fact(DisplayName = "TC-SEEDRESET-003: AllowReset = false hoặc thiếu -> từ chối reset")]
    public void TC_SEEDRESET_003_AllowResetDisabled_RejectsReset()
    {
        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "ShiftSchedulingDb_Demo",
            actualInitialCatalog: "ShiftSchedulingDb_Demo",
            isDemoDataEnabled: true,
            isAllowResetEnabled: false);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.AllowResetDisabled, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-004: DemoData bị vô hiệu hóa -> từ chối reset")]
    public void TC_SEEDRESET_004_DemoDataDisabled_RejectsReset()
    {
        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "ShiftSchedulingDb_Demo",
            actualInitialCatalog: "ShiftSchedulingDb_Demo",
            isDemoDataEnabled: false,
            isAllowResetEnabled: true);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.DemoDataDisabled, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-005: Tên database xác nhận không khớp InitialCatalog -> từ chối reset")]
    public void TC_SEEDRESET_005_DatabaseNameMismatch_RejectsReset()
    {
        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "OtherDb_Demo",
            actualInitialCatalog: "ShiftSchedulingDb_Demo",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.DatabaseNameMismatch, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-006: Database chính ShiftSchedulingDb không có hậu tố _Demo -> từ chối")]
    public void TC_SEEDRESET_006_ProductionDatabaseWithoutDemoSuffix_RejectsReset()
    {
        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "ShiftSchedulingDb",
            actualInitialCatalog: "ShiftSchedulingDb",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.MissingDemoSuffix, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-007: Database kiểm thử tích hợp ShiftSchedulingDb_IntegrationTest -> từ chối")]
    public void TC_SEEDRESET_007_IntegrationTestDatabase_RejectsReset()
    {
        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "ShiftSchedulingDb_IntegrationTest",
            actualInitialCatalog: "ShiftSchedulingDb_IntegrationTest",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.MissingDemoSuffix, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-008: InitialCatalog rỗng hoặc null -> từ chối reset")]
    public void TC_SEEDRESET_008_EmptyInitialCatalog_RejectsReset()
    {
        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "ShiftSchedulingDb_Demo",
            actualInitialCatalog: "",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.EmptyInitialCatalog, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-009: Tham số --reset-demo= để trống tên database -> từ chối reset")]
    public void TC_SEEDRESET_009_EmptyResetDatabaseName_RejectsReset()
    {
        var args = new[] { "--reset-demo=" };
        var isRequested = DemoDataResetCommand.TryExtractResetArgument(args, out var dbName);

        Assert.True(isRequested);
        Assert.Equal("", dbName);

        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: dbName,
            actualInitialCatalog: "ShiftSchedulingDb_Demo",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.False(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.EmptyResetDatabaseName, validation.Status);
    }

    [Fact(DisplayName = "TC-SEEDRESET-010: Hậu tố _Demo và tên database được so sánh không phân biệt hoa thường")]
    public void TC_SEEDRESET_010_CaseInsensitiveDemoSuffixAndName_PermitsReset()
    {
        var validation = DemoDataResetCommand.ValidateResetPreconditions(
            isResetRequested: true,
            requestedDatabaseName: "shiftschedulingdb_demo",
            actualInitialCatalog: "SHIFTSCHEDULINGDB_DEMO",
            isDemoDataEnabled: true,
            isAllowResetEnabled: true);

        Assert.True(validation.IsValid);
        Assert.Equal(DemoResetValidationStatus.Valid, validation.Status);
        Assert.Equal("SHIFTSCHEDULINGDB_DEMO", validation.TargetDatabaseName);
    }

    [Fact(DisplayName = "TC-SEEDRESET-011: FilterCommandLineArgs loại bỏ --reset-demo và giữ nguyên các args khác")]
    public void TC_SEEDRESET_011_FilterCommandLineArgs_PreservesOtherArguments()
    {
        var rawArgs = new[]
        {
            "--urls=http://localhost:5000",
            "--reset-demo=ShiftSchedulingDb_Demo",
            "--environment=Development"
        };

        var filtered = DemoDataResetCommand.FilterCommandLineArgs(rawArgs);

        Assert.Equal(2, filtered.Length);
        Assert.Contains("--urls=http://localhost:5000", filtered);
        Assert.Contains("--environment=Development", filtered);
        Assert.DoesNotContain(filtered, a => a.StartsWith("--reset-demo=", StringComparison.OrdinalIgnoreCase));
    }
}
