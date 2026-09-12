using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    /// <summary>
    /// Xây dựng toàn bộ tập dữ liệu mẫu trong bộ nhớ phục vụ tạo dữ liệu và kiểm thử.
    /// </summary>
    public static DemoDataSet BuildDemoDataSet(
        DateOnly today,
        IPasswordHashService passwordHashService,
        IClock clock)
    {
        var dataSet = new DemoDataSet();
        var (pastWeekStart, readyWeekStart, futureWeekStart) = CalculatePeriodDates(today);
        var nowUtc = clock.UtcNow;

        // Khởi tạo tài khoản, nhân viên và lịch đăng ký mẫu hàng tuần
        var allEmployees = BuildAccountsAndEmployees(dataSet, nowUtc, passwordHashService);

        // Khởi tạo kỳ đã công bố trong quá khứ kèm phân công ca hoàn chỉnh
        BuildScenarioB(dataSet, pastWeekStart, allEmployees, nowUtc, clock);

        // Khởi tạo kỳ sẵn sàng xếp lịch tuần tới (đã hết hạn đăng ký, chưa có bảng phân công)
        BuildScenarioReady(dataSet, readyWeekStart, allEmployees, nowUtc, clock);

        // Khởi tạo kỳ đang mở đăng ký trong tương lai kèm lịch rảnh của nhân viên
        BuildScenarioA(dataSet, futureWeekStart, allEmployees, nowUtc, clock);

        return dataSet;
    }
}
