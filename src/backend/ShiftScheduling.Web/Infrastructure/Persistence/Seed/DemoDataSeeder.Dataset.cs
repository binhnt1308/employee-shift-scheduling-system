using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    /// <summary>
    /// Xây dựng tập dữ liệu mẫu 4 kỳ tương ứng với 4 giai đoạn vòng đời thực tế:
    /// 1. Kỳ tuần trước: Đã đóng (Closed) - xem lịch sử & báo cáo.
    /// 2. Kỳ tuần hiện tại: Đã công bố (Published) - UI suy ra Đang áp dụng.
    /// 3. Kỳ tuần sắp đến: Hết hạn đăng ký (Draft) - quản lý xếp lịch / OR-Tools.
    /// 4. Kỳ tuần xa hơn: Đang mở đăng ký (No WorkSchedule) - khóa phân công sớm.
    /// </summary>
    public static DemoDataSet BuildFourPeriodDemoDataSet(
        DateOnly today,
        IPasswordHashService passwordHashService,
        IClock clock)
    {
        var dataSet = new DemoDataSet();
        var (prevWeekStart, currWeekStart, nextWeekStart, followWeekStart) = CalculateFourPeriodDates(today);
        var nowUtc = clock.UtcNow;

        var allEmployees = BuildAccountsAndEmployees(dataSet, nowUtc, passwordHashService);

        // Period 1: Tuần trước - Lịch sử đã đóng
        BuildPeriod1Closed(dataSet, prevWeekStart, allEmployees, nowUtc, clock);

        // Period 2: Tuần hiện tại - Đang áp dụng
        BuildPeriod2Published(dataSet, currWeekStart, allEmployees, nowUtc, clock);

        // Period 3: Tuần sắp đến - Hết hạn đăng ký, sẵn sàng xếp lịch (Draft, 0 ca)
        BuildScenarioReady(dataSet, nextWeekStart, allEmployees, nowUtc, clock);
        var period3 = dataSet.Periods.First(p => p.WeekStartDate == nextWeekStart);
        var draftSchedule = new WorkSchedule
        {
            SchedulePeriod = period3,
            ScheduleName = $"Dự thảo lịch tuần {nextWeekStart:dd/MM}",
            Status = ScheduleStatus.Draft,
            IsGeneratedByORTools = false,
            CreatedByAccountId = 2,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
        dataSet.WorkSchedules.Add(draftSchedule);

        // Period 4: Tuần xa hơn - Đang mở đăng ký, chưa có WorkSchedule để khóa xếp lịch sớm
        BuildScenarioA(dataSet, followWeekStart, allEmployees, nowUtc, clock);

        return dataSet;
    }

    /// <summary>
    /// Xây dựng tập dữ liệu mẫu trong bộ nhớ (phương thức tương thích ngược).
    /// </summary>
    public static DemoDataSet BuildDemoDataSet(
        DateOnly today,
        IPasswordHashService passwordHashService,
        IClock clock)
    {
        var dataSet = new DemoDataSet();
        var (pastWeekStart, readyWeekStart, futureWeekStart) = CalculatePeriodDates(today);
        var nowUtc = clock.UtcNow;

        var allEmployees = BuildAccountsAndEmployees(dataSet, nowUtc, passwordHashService);

        // Khởi tạo kỳ đã công bố trong quá khứ kèm phân công ca hoàn chỉnh
        BuildScenarioB(dataSet, pastWeekStart, allEmployees, nowUtc, clock);

        // Khởi tạo kỳ sẵn sàng xếp lịch tuần tới (đã hết hạn đăng ký)
        BuildScenarioReady(dataSet, readyWeekStart, allEmployees, nowUtc, clock);

        // Khởi tạo kỳ đang mở đăng ký trong tương lai kèm lịch rảnh của nhân viên
        BuildScenarioA(dataSet, futureWeekStart, allEmployees, nowUtc, clock);

        return dataSet;
    }
}
