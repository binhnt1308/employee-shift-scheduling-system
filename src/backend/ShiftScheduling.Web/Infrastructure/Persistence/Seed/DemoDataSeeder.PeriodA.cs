using ShiftScheduling.Common.Time;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    private static void BuildScenarioA(
        DemoDataSet dataSet,
        DateOnly futureWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        IClock clock)
    {
        // Kỳ lịch trong tương lai đang mở đăng ký
        var futureWeekEnd = futureWeekStart.AddDays(6);
        var futureDeadlineUtc = clock.ToUtcFromVietnamTime(futureWeekStart.AddDays(-1).ToDateTime(new TimeOnly(23, 59, 59)));

        var periodA = new SchedulePeriod
        {
            PeriodName = $"Kỳ lịch {futureWeekStart:dd/MM/yyyy} - {futureWeekEnd:dd/MM/yyyy} (Demo đăng ký)",
            WeekStartDate = futureWeekStart,
            WeekEndDate = futureWeekEnd,
            AvailabilityDeadline = futureDeadlineUtc,
            RegistrationStatus = RegistrationStatus.Open,
            CreatedByAccountId = 2,
            CreatedAt = nowUtc
        };
        dataSet.Periods.Add(periodA);

        // 21 định biên nhân sự cho Period A (7 ngày x 3 ca: 1 Bán hàng, 1 Thu ngân, tối thiểu 1 Level 2+)
        for (int d = 0; d < 7; d++)
        {
            var date = futureWeekStart.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                dataSet.StaffingRequirements.Add(new StaffingRequirement
                {
                    SchedulePeriod = periodA,
                    WorkDate = date,
                    ShiftTypeId = s,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }

        // Đăng ký lịch rảnh phong phú (cả Bình thường và Ưa thích, đủ nhân sự cho thuật toán xếp lịch)
        SeedPeriodAAvailabilities(dataSet, periodA, futureWeekStart, allEmployees, nowUtc);
    }

    private static void SeedPeriodAAvailabilities(
        DemoDataSet dataSet,
        SchedulePeriod periodA,
        DateOnly futureWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc)
    {
        var emp01 = allEmployees[0]; // NV01: Bán hàng toàn thời gian (Level 2)
        var emp02 = allEmployees[1]; // NV02: Chiêm Trung Hậu - Bán hàng toàn thời gian (Level 2)
        var emp03 = allEmployees[2]; // NV03: Nguyễn Thanh Bình - Thu ngân toàn thời gian (Level 3)
        var emp04 = allEmployees[3]; // NV04: Bán hàng toàn thời gian (Level 2)
        var emp05 = allEmployees[4]; // NV05: Thu ngân bán thời gian (Level 2)
        var emp06 = allEmployees[5]; // NV06: Thu ngân bán thời gian (Level 1)
        var emp07 = allEmployees[6]; // NV07: Thu ngân toàn thời gian (Level 3)
        var emp08 = allEmployees[7]; // NV08: Bán hàng bán thời gian (Level 1)

        for (int d = 0; d < 7; d++)
        {
            var date = futureWeekStart.AddDays(d);

            // NV01: Rảnh ca 1 (Ưa thích), ca 2 (Bình thường) từ Thứ 2 đến Thứ 7
            if (d < 6)
            {
                AddAvail(dataSet, emp01, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp01, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            }

            // NV02 (Chiêm Trung Hậu): Rảnh ca 2 và 3 (Ưa thích), ca 1 (Bình thường) từ Thứ 3 đến Chủ nhật; Thứ 2 rảnh ca 2 (Bình thường), ca 3 (Ưa thích)
            if (d >= 1)
            {
                AddAvail(dataSet, emp02, periodA, date, 2, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp02, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp02, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
            }
            else
            {
                AddAvail(dataSet, emp02, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
                AddAvail(dataSet, emp02, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            }

            // NV04: Rảnh ca 2 (Bình thường), ca 3 (Ưa thích), ca 1 (Ưa thích vào cuối tuần)
            AddAvail(dataSet, emp04, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            AddAvail(dataSet, emp04, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            if (d == 5 || d == 6)
            {
                AddAvail(dataSet, emp04, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
            }

            // NV08: Rảnh ca 1 (Bình thường), ca 3 (Ưa thích) các ngày trừ Thứ 3
            if (d != 1)
            {
                AddAvail(dataSet, emp08, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
                AddAvail(dataSet, emp08, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            }

            // NV03 (Bình): Rảnh ca 2 (Bình thường), ca 3 (Ưa thích), ca 1 (Bình thường từ Thứ 2 đến Thứ 6)
            AddAvail(dataSet, emp03, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            AddAvail(dataSet, emp03, periodA, date, 3, AvailabilityPreferenceLevel.Preferred, nowUtc);
            if (d < 5)
            {
                AddAvail(dataSet, emp03, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
            }

            // NV07: Rảnh ca 1 và ca 2 (Ưa thích), ca 3 (Bình thường)
            AddAvail(dataSet, emp07, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
            AddAvail(dataSet, emp07, periodA, date, 2, AvailabilityPreferenceLevel.Preferred, nowUtc);
            AddAvail(dataSet, emp07, periodA, date, 3, AvailabilityPreferenceLevel.Normal, nowUtc);

            // NV05: Rảnh ca 1 (Ưa thích), ca 2 (Bình thường) từ Thứ 2 đến Thứ 7
            if (d < 6)
            {
                AddAvail(dataSet, emp05, periodA, date, 1, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp05, periodA, date, 2, AvailabilityPreferenceLevel.Normal, nowUtc);
            }

            // NV06: Rảnh ca 2 (Ưa thích), ca 3 và ca 1 (Bình thường) các ngày trừ Thứ 7
            if (d != 5)
            {
                AddAvail(dataSet, emp06, periodA, date, 2, AvailabilityPreferenceLevel.Preferred, nowUtc);
                AddAvail(dataSet, emp06, periodA, date, 3, AvailabilityPreferenceLevel.Normal, nowUtc);
                AddAvail(dataSet, emp06, periodA, date, 1, AvailabilityPreferenceLevel.Normal, nowUtc);
            }
        }
    }

    private static void AddAvail(
        DemoDataSet dataSet,
        Employee emp,
        SchedulePeriod period,
        DateOnly date,
        int shiftTypeId,
        AvailabilityPreferenceLevel prefLevel,
        DateTime nowUtc)
    {
        dataSet.Availabilities.Add(new Availability
        {
            Employee = emp,
            SchedulePeriod = period,
            WorkDate = date,
            ShiftTypeId = shiftTypeId,
            PreferenceLevel = prefLevel,
            RegisteredAt = nowUtc
        });
    }
}
