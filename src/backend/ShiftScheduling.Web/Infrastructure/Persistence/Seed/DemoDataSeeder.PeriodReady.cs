using ShiftScheduling.Common.Time;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    private static void BuildScenarioReady(
        DemoDataSet dataSet,
        DateOnly readyWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        IClock clock)
    {
        // Kỳ lịch tuần tới đã hết hạn đăng ký, sẵn sàng để quản lý xếp lịch
        var readyWeekEnd = readyWeekStart.AddDays(6);
        var readyDeadlineUtc = nowUtc.AddMinutes(-1);

        var periodReady = new SchedulePeriod
        {
            PeriodName = $"Kỳ lịch {readyWeekStart:dd/MM/yyyy} - {readyWeekEnd:dd/MM/yyyy} (Demo sẵn sàng xếp lịch)",
            WeekStartDate = readyWeekStart,
            WeekEndDate = readyWeekEnd,
            AvailabilityDeadline = readyDeadlineUtc,
            RegistrationStatus = RegistrationStatus.Open,
            CreatedByAccountId = 2,
            CreatedAt = nowUtc
        };
        dataSet.Periods.Add(periodReady);

        // 21 định biên nhân sự cho kỳ sẵn sàng xếp lịch
        AddPeriodStaffingRequirements(dataSet, periodReady, readyWeekStart);

        // Đăng ký lịch rảnh phong phú đảm bảo OR-Tools giải thành công
        SeedPeriodAvailabilities(dataSet, periodReady, readyWeekStart, allEmployees, nowUtc);
    }
}
