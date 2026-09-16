using ShiftScheduling.Common.Time;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    private static void BuildPeriod1Closed(
        DemoDataSet dataSet,
        DateOnly pastWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        IClock clock)
    {
        BuildPublishedOrClosedPeriod(
            dataSet, pastWeekStart, allEmployees, nowUtc, clock,
            ClosedDemoPeriodMarker, ScheduleStatus.Closed, isHistoricalWithAdjustment: true);
    }

    private static void BuildPeriod2Published(
        DemoDataSet dataSet,
        DateOnly currentWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        IClock clock)
    {
        BuildPublishedOrClosedPeriod(
            dataSet, currentWeekStart, allEmployees, nowUtc, clock,
            PublishedDemoPeriodMarker, ScheduleStatus.Published, isHistoricalWithAdjustment: false);
    }

    private static void BuildScenarioB(
        DemoDataSet dataSet,
        DateOnly pastWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        IClock clock)
    {
        BuildPublishedOrClosedPeriod(
            dataSet, pastWeekStart, allEmployees, nowUtc, clock,
            PublishedDemoPeriodMarker, ScheduleStatus.Published, isHistoricalWithAdjustment: true);
    }

    private static void BuildPublishedOrClosedPeriod(
        DemoDataSet dataSet,
        DateOnly weekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        IClock clock,
        string periodMarker,
        ScheduleStatus scheduleStatus,
        bool isHistoricalWithAdjustment)
    {
        var weekEnd = weekStart.AddDays(6);
        var deadlineUtc = clock.ToUtcFromVietnamTime(
            weekStart.AddDays(-3).ToDateTime(new TimeOnly(23, 59, 59)));

        var period = new SchedulePeriod
        {
            PeriodName = $"Kỳ lịch {weekStart:dd/MM/yyyy} - {weekEnd:dd/MM/yyyy} {periodMarker}",
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            AvailabilityDeadline = deadlineUtc,
            RegistrationStatus = RegistrationStatus.Closed,
            CreatedByAccountId = 2,
            CreatedAt = nowUtc.AddDays(-14)
        };
        dataSet.Periods.Add(period);

        AddPeriodStaffingRequirements(dataSet, period, weekStart);

        var assignments = BuildScenarioBAssignments(
            weekStart, allEmployees, nowUtc, isHistoricalWithAdjustment);

        foreach (var assign in assignments)
        {
            var isPreferred = (assign.Employee == allEmployees[1] && assign.ShiftTypeId == 3)
                              || (assign.Employee == allEmployees[2] && assign.ShiftTypeId == 3);

            dataSet.Availabilities.Add(new Availability
            {
                Employee = assign.Employee,
                SchedulePeriod = period,
                WorkDate = assign.WorkDate,
                ShiftTypeId = assign.ShiftTypeId,
                PreferenceLevel = isPreferred
                    ? AvailabilityPreferenceLevel.Preferred
                    : AvailabilityPreferenceLevel.Normal,
                RegisteredAt = deadlineUtc.AddDays(-2)
            });
        }

        var workSchedule = new WorkSchedule
        {
            SchedulePeriod = period,
            ScheduleName = $"Lịch làm việc chính thức tuần {weekStart:dd/MM}",
            Status = scheduleStatus,
            IsGeneratedByORTools = true,
            CreatedByAccountId = 2,
            PublishedByAccountId = 2,
            CreatedAt = deadlineUtc.AddHours(2),
            PublishedAt = clock.ToUtcFromVietnamTime(
                weekStart.AddDays(-1).ToDateTime(new TimeOnly(18, 0))),
            UpdatedAt = nowUtc
        };
        dataSet.WorkSchedules.Add(workSchedule);

        foreach (var assign in assignments)
        {
            assign.WorkSchedule = workSchedule;
        }
        dataSet.ShiftAssignments.AddRange(assignments);
    }

    private static List<ShiftAssignment> BuildScenarioBAssignments(
        DateOnly weekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        bool isHistoricalWithAdjustment = true)
    {
        var emp01 = allEmployees[0];
        var emp02 = allEmployees[1];
        var emp03 = allEmployees[2];
        var emp04 = allEmployees[3];
        var emp05 = allEmployees[4];
        var emp06 = allEmployees[5];
        var emp07 = allEmployees[6];
        var emp08 = allEmployees[7];

        var assignments = new List<ShiftAssignment>();

        // Ma trận phân công Bán hàng (7 ngày x 3 ca = 21 ca):
        // emp01 (Sales FT L2): 6 ca (Thứ 2 đến Thứ 7 ca 1, Chủ nhật nghỉ)
        // emp02 (Sales FT L2 - Hậu): 6 ca (T2,3,5,6 ca 3; T4 ca 2; CN ca 1; T7 nghỉ)
        // emp04 (Sales FT L2): 6 ca (Thứ 2, 3, 5, 6, 7, Chủ nhật ca 2; Thứ 4 nghỉ)
        // emp08 (Sales PT L1): 3 ca (Thứ 4, 7, Chủ nhật ca 3)
        var salesPlan = new Employee[,]
        {
            { emp01, emp04, emp02 }, // Day 0 (Mon): S1(emp01), S2(emp04), S3(emp02)
            { emp01, emp04, emp02 }, // Day 1 (Tue): S1(emp01), S2(emp04), S3(emp02)
            { emp01, emp02, emp08 }, // Day 2 (Wed): S1(emp01), S2(emp02), S3(emp08) (emp04 nghỉ)
            { emp01, emp04, emp02 }, // Day 3 (Thu): S1(emp01), S2(emp04), S3(emp02)
            { emp01, emp04, emp02 }, // Day 4 (Fri): S1(emp01), S2(emp04), S3(emp02) -> NC-07 Chiêm Trung Hậu
            { emp01, emp04, emp08 }, // Day 5 (Sat): S1(emp01), S2(emp04), S3(emp08) (emp02 nghỉ)
            { emp02, emp04, emp08 }  // Day 6 (Sun): S1(emp02), S2(emp04), S3(emp08) (emp01 nghỉ)
        };

        // Ma trận phân công Thu ngân (7 ngày x 3 ca = 21 ca):
        // emp03 (Cashier FT L3 - Bình): 6 ca (Thứ 2, 3, 4, 6, 7, Chủ nhật ca 3; Thứ 5 nghỉ)
        // emp07 (Cashier FT L3): 6 ca (Thứ 2 đến Thứ 6 và Chủ nhật ca 2; Thứ 7 nghỉ)
        // emp05 (Cashier PT L2): 5 ca (Thứ 2, 4, 5, 6, 7 ca 1; Thứ 3 và Chủ nhật nghỉ)
        // emp06 (Cashier PT L1): 4 ca (T3 ca 1, T5 ca 3, T7 ca 2, CN ca 1; T2,4,6 nghỉ)
        var cashierPlan = new Employee[,]
        {
            { emp05, emp07, emp03 }, // Day 0 (Mon): S1(emp05), S2(emp07), S3(emp03)
            { emp06, emp07, emp03 }, // Day 1 (Tue): S1(emp06), S2(emp07), S3(emp03)
            { emp05, emp07, emp03 }, // Day 2 (Wed): S1(emp05), S2(emp07), S3(emp03)
            { emp05, emp07, emp06 }, // Day 3 (Thu): S1(emp05), S2(emp07), S3(emp06) (emp03 nghỉ)
            { emp05, emp07, emp03 }, // Day 4 (Fri): S1(emp05), S2(emp07), S3(emp03)
            { emp05, emp06, emp03 }, // Day 5 (Sat): S1(emp05), S2(emp06), S3(emp03) (emp07 nghỉ)
            { emp06, emp07, emp03 }  // Day 6 (Sun): S1(emp06), S2(emp07), S3(emp03)
        };

        for (int d = 0; d < 7; d++)
        {
            var date = weekStart.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                var salesEmp = salesPlan[d, s - 1];
                var salesAssign = new ShiftAssignment
                {
                    Employee = salesEmp,
                    WorkDate = date,
                    ShiftTypeId = s,
                    Source = AssignmentSource.ORTools,
                    CreatedAt = nowUtc.AddDays(-10)
                };

                // NC-07: Điều chỉnh giờ làm thực tế của NV02 ca tối T6 nếu là kỳ lịch sử
                if (isHistoricalWithAdjustment && d == 4 && s == 3 && salesEmp == emp02)
                {
                    salesAssign.ActualWorkedHours = 3.50m;
                    salesAssign.WorkdayAdjustmentReason = "về sớm 30 phút theo điều chỉnh của quản lý";
                    salesAssign.UpdatedAt = nowUtc.AddDays(-3);
                }

                assignments.Add(salesAssign);

                var cashierEmp = cashierPlan[d, s - 1];
                var cashierAssign = new ShiftAssignment
                {
                    Employee = cashierEmp,
                    WorkDate = date,
                    ShiftTypeId = s,
                    Source = AssignmentSource.ORTools,
                    CreatedAt = nowUtc.AddDays(-10)
                };
                assignments.Add(cashierAssign);
            }
        }

        return assignments;
    }
}
