namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    /// <summary>
    /// Quyết định tạo dữ liệu demo dựa trên trạng thái hiện tại của database.
    /// </summary>
    public enum SeedDecision
    {
        ProceedWithFreshSeed,
        SkipAlreadySeeded,
        SkipLegacyDemoDataset,
        SkipExistingBusinessData
    }

    /// <summary>
    /// Xác định có nên tạo dữ liệu demo dựa trên trạng thái hiện tại của database.
    /// Nhận diện SEED IDENTITY dựa trên exact demo marker constants (EndsWith).
    /// Không kiểm tra dữ liệu mutable để đảm bảo không crash startup sau khi người dùng thao tác.
    /// </summary>
    public static SeedDecision EvaluateSeedPrecondition(
        int totalEmployees,
        IEnumerable<string> existingPeriodNames)
    {
        var list = existingPeriodNames.ToList();
        bool Has(string m) => list.Any(n => n.EndsWith(m, StringComparison.OrdinalIgnoreCase));
        var hasClosed = Has(ClosedDemoPeriodMarker);
        var hasPublished = Has(PublishedDemoPeriodMarker);
        var hasReady = Has(ReadyToScheduleDemoPeriodMarker);
        var hasRegistration = Has(RegistrationDemoPeriodMarker);
        var hasLegacy = Has(LegacyRegistrationDemoPeriodMarker);

        // 1. Không chứa bất kỳ marker demo nào
        if (!hasClosed && !hasPublished && !hasReady && !hasRegistration && !hasLegacy)
        {
            return (totalEmployees == 0 && list.Count == 0)
                ? SeedDecision.ProceedWithFreshSeed
                : SeedDecision.SkipExistingBusinessData;
        }

        // 2. Bộ demo mới 4 kỳ hoàn chỉnh
        if (hasClosed && hasPublished && hasReady && hasRegistration && !hasLegacy)
        {
            return SeedDecision.SkipAlreadySeeded;
        }

        // 3. Bộ demo 3 kỳ tương thích
        if (!hasClosed && hasPublished && hasReady && hasRegistration && !hasLegacy)
        {
            return SeedDecision.SkipAlreadySeeded;
        }

        // 4. Bộ demo legacy 2 kỳ hoàn chỉnh
        if (hasPublished && hasLegacy && !hasReady && !hasRegistration && !hasClosed)
        {
            return SeedDecision.SkipLegacyDemoDataset;
        }

        // 5. Tổ hợp marker demo không hợp lệ hoặc thiếu kỳ -> Báo lỗi bảo vệ dữ liệu
        throw new InvalidOperationException(
            "Partial demo dataset detected: Database contains demo period markers but lacks complete demo set. " +
            $"Closed: {hasClosed}, Published: {hasPublished}, Ready: {hasReady}, " +
            $"Registration: {hasRegistration}, Legacy: {hasLegacy}. " +
            "Seeder will not proceed to prevent data corruption.");
    }

    /// <summary>
    /// Overload tương thích ngược cho EvaluateSeedPrecondition.
    /// Bỏ qua các tham số nghiệp vụ mutable.
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
        int publishedAssignmentCount = 42,
        int readyStaffingCount = 21,
        int readyAvailabilityCount = 1,
        bool readyHasNormalAvailability = true,
        bool readyHasPreferredAvailability = true,
        bool readyHasWorkSchedule = false,
        int readyAssignmentCount = 0,
        bool registrationHasWorkSchedule = false,
        int registrationAssignmentCount = 0,
        int legacyRegistrationStaffingCount = 21,
        int legacyRegistrationAvailabilityCount = 1,
        bool legacyRegistrationHasNormalAvailability = true,
        bool legacyRegistrationHasPreferredAvailability = true,
        bool legacyRegistrationHasWorkSchedule = false,
        int legacyRegistrationAssignmentCount = 0)
    {
        return EvaluateSeedPrecondition(totalEmployees, existingPeriodNames);
    }

    /// <summary>
    /// Kiểm tra xem database đã có đầy đủ bộ dữ liệu mẫu hay chưa để tránh tạo lặp.
    /// </summary>
    public static bool IsAlreadySeeded(
        IEnumerable<string> existingEmployeeCodes,
        IEnumerable<string> existingPeriodNames,
        int registrationStaffingCount = 21,
        int registrationAvailabilityCount = 1,
        bool hasNormalAvailability = true,
        bool hasPreferredAvailability = true,
        bool hasPublishedWorkSchedule = true,
        int publishedAssignmentCount = 42,
        int readyStaffingCount = 21,
        int readyAvailabilityCount = 1,
        bool readyHasNormalAvailability = true,
        bool readyHasPreferredAvailability = true,
        bool readyHasWorkSchedule = false,
        int readyAssignmentCount = 0,
        bool registrationHasWorkSchedule = false,
        int registrationAssignmentCount = 0,
        int legacyRegistrationStaffingCount = 21,
        int legacyRegistrationAvailabilityCount = 1,
        bool legacyRegistrationHasNormalAvailability = true,
        bool legacyRegistrationHasPreferredAvailability = true,
        bool legacyRegistrationHasWorkSchedule = false,
        int legacyRegistrationAssignmentCount = 0)
    {
        var periodList = existingPeriodNames.ToList();
        var decision = EvaluateSeedPrecondition(0, periodList);
        return decision == SeedDecision.SkipAlreadySeeded || decision == SeedDecision.SkipLegacyDemoDataset;
    }
}
