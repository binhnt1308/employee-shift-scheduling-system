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
    /// Không kiểm tra dữ liệu nghiệp vụ mutable để đảm bảo không crash startup sau khi người dùng thao tác.
    /// </summary>
    public static SeedDecision EvaluateSeedPrecondition(
        int totalEmployees,
        IEnumerable<string> existingPeriodNames)
    {
        var periodList = existingPeriodNames.ToList();
        var hasPublishedPeriod = periodList.Any(name => name.EndsWith(PublishedDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));
        var hasReadyPeriod = periodList.Any(name => name.EndsWith(ReadyToScheduleDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));
        var hasRegistrationPeriod = periodList.Any(name => name.EndsWith(RegistrationDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));
        var hasLegacyRegistrationPeriod = periodList.Any(name => name.EndsWith(LegacyRegistrationDemoPeriodMarker, StringComparison.OrdinalIgnoreCase));

        var hasAnyDemoPeriod = hasPublishedPeriod || hasReadyPeriod || hasRegistrationPeriod || hasLegacyRegistrationPeriod;

        // 1. Không chứa bất kỳ marker demo nào
        if (!hasAnyDemoPeriod)
        {
            if (totalEmployees == 0 && periodList.Count == 0)
            {
                return SeedDecision.ProceedWithFreshSeed;
            }

            // Database đã có nhân viên hoặc kỳ lịch nghiệp vụ thực tế
            return SeedDecision.SkipExistingBusinessData;
        }

        // 2. Bộ demo mới 3 kỳ hoàn chỉnh
        if (hasPublishedPeriod && hasReadyPeriod && hasRegistrationPeriod && !hasLegacyRegistrationPeriod)
        {
            return SeedDecision.SkipAlreadySeeded;
        }

        // 3. Bộ demo legacy 2 kỳ hoàn chỉnh
        if (hasPublishedPeriod && hasLegacyRegistrationPeriod && !hasReadyPeriod && !hasRegistrationPeriod)
        {
            return SeedDecision.SkipLegacyDemoDataset;
        }

        // 4. Tổ hợp marker demo không hợp lệ hoặc thiếu kỳ -> Báo lỗi bảo vệ toàn vẹn dữ liệu
        throw new InvalidOperationException(
            $"Partial demo dataset detected: Database contains demo period markers but lacks a valid complete demo dataset. " +
            $"Published demo period: {hasPublishedPeriod}, " +
            $"Ready to schedule demo period: {hasReadyPeriod}, " +
            $"Registration demo period: {hasRegistrationPeriod}, " +
            $"Legacy registration demo period: {hasLegacyRegistrationPeriod}. " +
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
    /// Kiểm tra xem database đã có đầy đủ bộ dữ liệu mẫu (3 kỳ mới hoặc legacy 2 kỳ) hay chưa để đảm bảo không tạo lặp lại.
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
