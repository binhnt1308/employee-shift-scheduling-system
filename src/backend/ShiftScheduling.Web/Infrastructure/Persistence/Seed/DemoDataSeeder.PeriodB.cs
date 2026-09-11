using ShiftScheduling.Common.Time;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static partial class DemoDataSeeder
{
    private static void BuildScenarioB(
        DemoDataSet dataSet,
        DateOnly pastWeekStart,
        List<Employee> allEmployees,
        DateTime nowUtc,
        IClock clock)
    {
        var emp01 = allEmployees[0];
        var emp02 = allEmployees[1];
        var emp03 = allEmployees[2];
        var emp04 = allEmployees[3];
        var emp05 = allEmployees[4];
        var emp06 = allEmployees[5];
        var emp07 = allEmployees[6];
        var emp08 = allEmployees[7];

        // Kỳ lịch đã công bố trong quá khứ (tuần trước, đã kết thúc đăng ký)
        var pastWeekEnd = pastWeekStart.AddDays(6);
        var pastDeadlineUtc = clock.ToUtcFromVietnamTime(pastWeekStart.AddDays(-1).ToDateTime(new TimeOnly(23, 59, 59)));

        var periodB = new SchedulePeriod
        {
            PeriodName = $"Kỳ lịch {pastWeekStart:dd/MM/yyyy} - {pastWeekEnd:dd/MM/yyyy} (Demo đã công bố)",
            WeekStartDate = pastWeekStart,
            WeekEndDate = pastWeekEnd,
            AvailabilityDeadline = pastDeadlineUtc,
            RegistrationStatus = RegistrationStatus.Closed,
            CreatedByAccountId = 2,
            CreatedAt = nowUtc.AddDays(-14)
        };
        dataSet.Periods.Add(periodB);

        // 21 định biên nhân sự cho Period B (7 ngày x 3 ca: 1 Bán hàng, 1 Thu ngân, tối thiểu 1 Level 2+)
        for (int d = 0; d < 7; d++)
        {
            var date = pastWeekStart.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                dataSet.StaffingRequirements.Add(new StaffingRequirement
                {
                    SchedulePeriod = periodB,
                    WorkDate = date,
                    ShiftTypeId = s,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }

        // Đăng ký lịch rảnh tương ứng với 42 lượt phân công ca của Period B
        var periodBAssignments = BuildScenarioBAssignments(
            pastWeekStart, emp01, emp02, emp03, emp04, emp05, emp06, emp07, emp08, nowUtc);

        foreach (var assign in periodBAssignments)
        {
            var isPreferred = (assign.Employee == emp02 && assign.ShiftTypeId == 3)
                              || (assign.Employee == emp03 && assign.ShiftTypeId == 3);

            dataSet.Availabilities.Add(new Availability
            {
                Employee = assign.Employee,
                SchedulePeriod = periodB,
                WorkDate = assign.WorkDate,
                ShiftTypeId = assign.ShiftTypeId,
                PreferenceLevel = isPreferred ? AvailabilityPreferenceLevel.Preferred : AvailabilityPreferenceLevel.Normal,
                RegisteredAt = pastDeadlineUtc.AddDays(-2)
            });
        }

        // Bảng phân công ca chính thức của Period B ở trạng thái đã công bố (Published)
        var workScheduleB = new WorkSchedule
        {
            SchedulePeriod = periodB,
            ScheduleName = $"Lịch làm việc chính thức tuần {pastWeekStart:dd/MM}",
            Status = ScheduleStatus.Published,
            IsGeneratedByORTools = true,
            CreatedByAccountId = 2,
            PublishedByAccountId = 2,
            CreatedAt = pastDeadlineUtc.AddHours(2),
            PublishedAt = pastWeekStart.AddDays(-1).ToDateTime(new TimeOnly(18, 0)),
            UpdatedAt = nowUtc
        };
        dataSet.WorkSchedules.Add(workScheduleB);

        foreach (var assign in periodBAssignments)
        {
            assign.WorkSchedule = workScheduleB;
        }
        dataSet.ShiftAssignments.AddRange(periodBAssignments);
    }

    private static List<ShiftAssignment> BuildScenarioBAssignments(
        DateOnly pastWeekStart,
        Employee emp01,
        Employee emp02,
        Employee emp03,
        Employee emp04,
        Employee emp05,
        Employee emp06,
        Employee emp07,
        Employee emp08,
        DateTime nowUtc)
    {
        var assignments = new List<ShiftAssignment>();

        // Ma trận phân công Bán hàng (7 ngày x 3 ca = 21 ca):
        // emp01 (Sales FT L2): 6 ca (Thứ 2 đến Thứ 7 ca 1, Chủ nhật nghỉ)
        // emp02 (Sales FT L2 - Chiêm Trung Hậu): 6 ca (Thứ 2, 3, 5, 6 ca 3; Thứ 4 ca 2; Chủ nhật ca 1; Thứ 7 nghỉ)
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
        // emp06 (Cashier PT L1): 4 ca (Thứ 3 ca 1, Thứ 5 ca 3, Thứ 7 ca 2, Chủ nhật ca 1; Thứ 2, 4, 6 nghỉ)
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
            var date = pastWeekStart.AddDays(d);
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

                // NC-07: Điều chỉnh giờ làm thực tế của Chiêm Trung Hậu vào ca tối Thứ Sáu
                if (d == 4 && s == 3 && salesEmp == emp02)
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
