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
        SkipExistingBusinessData
    }

    /// <summary>
    /// Xác định có nên tạo dữ liệu demo dựa trên trạng thái hiện tại của database.
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

        // Database không chứa kỳ lịch mẫu nào
        if (!hasAnyDemoPeriod)
        {
            if (totalEmployees == 0 && periodList.Count == 0)
            {
                return SeedDecision.ProceedWithFreshSeed;
            }

            // Không tạo dữ liệu demo nếu database đã có nhân viên hoặc kỳ lịch nghiệp vụ thực tế
            return SeedDecision.SkipExistingBusinessData;
        }

        // Database chứa kỳ lịch mẫu: kiểm tra tính toàn vẹn của bộ dữ liệu demo
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

        // Báo lỗi nếu phát hiện tập dữ liệu mẫu bị thiếu thành phần cốt lõi
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
    /// Kiểm tra xem database đã có đầy đủ bộ dữ liệu mẫu hay chưa để đảm bảo không tạo lặp lại.
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
}
