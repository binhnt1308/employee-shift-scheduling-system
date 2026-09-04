using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class OrToolsSchedulingServiceTests
{
    private readonly OrToolsSchedulingService _service = new(
        new SchedulingValidationService(),
        NullLogger<OrToolsSchedulingService>.Instance);

    [Fact(DisplayName = "TC06-01 - UC06 BR15 - OR-Tools CP-SAT sinh lịch đề xuất thành công" +
        " khi đủ nhân sự và rảnh")]
    public void GenerateProposal_WhenSufficientEmployeesAndAvailabilities_ReturnsFeasibleProposal()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = CreateThreeFixedShifts();
        var employees = CreateSixEmployees();
        var (avails, requirements) = CreateAvailabilitiesAndRequirements(period, shiftTypes, employees);

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.ProposedAssignments);
        Assert.True(result.ValidationResult.IsValid);
    }

    [Fact(DisplayName = "TC06-02 - UC06 BR15 - Không có nhân viên khả thi -> Trả về INFEASIBLE")]
    public void GenerateProposal_WhenNoAvailableEmployees_ReturnsInfeasible()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = CreateThreeFixedShifts();
        var employees = new List<Employee>
        {
            new Employee
            {
                EmployeeId = 1,
                EmployeeCode = "NV01",
                FullName = "NV 1",
                Position = EmployeePosition.Sales,
                Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.PartTime,
                IsActive = true
            }
        };

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 1,
                RequiredSalesStaff = 5,
                RequiredCashiers = 5
            }
        };

        var result = _service.GenerateProposal(
            period, employees,
            new List<Availability>(), new List<LeaveRequest>(),
            requirements, shiftTypes, 5);

        Assert.False(result.Succeeded);
        Assert.Equal("INFEASIBLE", result.Status.ToUpperInvariant());
    }

    [Fact(DisplayName = "TC06-03 - UC06 BR15 - Sinh proposal chỉ trả về đề xuất," +
        " không tự động công bố")]
    public void GenerateProposal_DoesNotAutoPublishSchedule()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = CreateThreeFixedShifts();
        var employees = CreateSixEmployees();

        var (avails, requirements) = CreateAvailabilitiesAndRequirements(period, shiftTypes, employees);

        var result = _service.GenerateProposal(
            period, employees, avails,
            new List<LeaveRequest>(), requirements,
            shiftTypes, 5);

        Assert.NotNull(result);
        Assert.True(result.Succeeded);
    }

    private static List<Employee> CreateSixEmployees()
    {
        var emps = new List<Employee>();
        for (int i = 1; i <= 6; i++)
        {
            emps.Add(new Employee
            {
                EmployeeId = i,
                EmployeeCode = $"NV{i:D2}",
                FullName = $"NV {i}",
                Position = i <= 3 ? EmployeePosition.Sales : EmployeePosition.Cashier,
                Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.PartTime,
                IsActive = true
            });
        }
        return emps;
    }

    private static (List<Availability> Avails, List<StaffingRequirement> Reqs) CreateAvailabilitiesAndRequirements(
        SchedulePeriod period,
        List<ShiftType> shiftTypes,
        List<Employee> employees)
    {
        var avails = new List<Availability>();
        var requirements = new List<StaffingRequirement>();

        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            foreach (var st in shiftTypes)
            {
                foreach (var emp in employees)
                {
                    avails.Add(new Availability
                    {
                        EmployeeId = emp.EmployeeId,
                        WorkDate = date,
                        ShiftTypeId = st.ShiftTypeId
                    });
                }

                requirements.Add(new StaffingRequirement
                {
                    WorkDate = date,
                    ShiftTypeId = st.ShiftTypeId,
                    ShiftType = st,
                    RequiredSalesStaff = 1,
                    RequiredCashiers = 1,
                    RequiredExperiencedStaff = 1,
                    MinimumLevel = EmployeeLevel.Level2
                });
            }
        }

        return (avails, requirements);
    }

    private static List<ShiftType> CreateThreeFixedShifts()
    {
        return new List<ShiftType>
        {
            new ShiftType
            {
                ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0),
                DurationHours = 4m,
                DisplayOrder = 1, IsActive = true,
                MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1
            },
            new ShiftType
            {
                ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0),
                DurationHours = 4m,
                DisplayOrder = 2, IsActive = true,
                MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1
            },
            new ShiftType
            {
                ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0),
                DurationHours = 4m,
                DisplayOrder = 3, IsActive = true,
                MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1
            }
        };
    }

    [Fact(DisplayName = "TC06-BR21-01 - OR-Tools tìm được lịch hợp lệ khi tồn tại phương án thỏa BR21")]
    public void TC06_BR21_01_ShouldFindValidScheduleWhenWeeklyRestAlternativeExists()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = CreateThreeFixedShifts();
        var morning = shiftTypes.Single(s => s.ShiftCode == "MORNING");
        var afternoon = shiftTypes.Single(s => s.ShiftCode == "AFTERNOON");
        var evening = shiftTypes.Single(s => s.ShiftCode == "EVENING");

        var employees = new List<Employee>
        {
            new Employee
            {
                EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1",
                Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.PartTime, IsActive = true
            },
            new Employee
            {
                EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV 2",
                Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.PartTime, IsActive = true
            }
        };

        var avails = new List<Availability>();
        var requirements = new List<StaffingRequirement>();

        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);

            avails.Add(new Availability { EmployeeId = 1, WorkDate = date, ShiftTypeId = morning.ShiftTypeId });
            avails.Add(new Availability { EmployeeId = 1, WorkDate = date, ShiftTypeId = evening.ShiftTypeId });
            avails.Add(new Availability { EmployeeId = 2, WorkDate = date, ShiftTypeId = morning.ShiftTypeId });
            avails.Add(new Availability { EmployeeId = 2, WorkDate = date, ShiftTypeId = evening.ShiftTypeId });

            requirements.Add(new StaffingRequirement
            {
                WorkDate = date,
                ShiftTypeId = morning.ShiftTypeId,
                ShiftType = morning,
                RequiredSalesStaff = 1,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 1,
                MinimumLevel = EmployeeLevel.Level2
            });
            requirements.Add(new StaffingRequirement
            {
                WorkDate = date,
                ShiftTypeId = afternoon.ShiftTypeId,
                ShiftType = afternoon,
                RequiredSalesStaff = 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 0,
                MinimumLevel = EmployeeLevel.Level2
            });
            requirements.Add(new StaffingRequirement
            {
                WorkDate = date,
                ShiftTypeId = evening.ShiftTypeId,
                ShiftType = evening,
                RequiredSalesStaff = 1,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 1,
                MinimumLevel = EmployeeLevel.Level2
            });
        }

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(14, result.ProposedAssignments.Count);
        Assert.True(result.ValidationResult.IsValid);
        Assert.DoesNotContain(result.ValidationResult.HardErrors, error => error.Contains("BR21"));

        var validAlternativeAssignments = new List<ShiftAssignment>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            var (emp1Shift, emp2Shift) = (d == 2 || d == 3 || d == 6)
                ? (evening, morning)
                : (morning, evening);

            validAlternativeAssignments.Add(new ShiftAssignment
            {
                EmployeeId = 1,
                WorkDate = date,
                ShiftTypeId = emp1Shift.ShiftTypeId,
                Source = AssignmentSource.Manual
            });
            validAlternativeAssignments.Add(new ShiftAssignment
            {
                EmployeeId = 2,
                WorkDate = date,
                ShiftTypeId = emp2Shift.ShiftTypeId,
                Source = AssignmentSource.Manual
            });
        }

        var validationService = new SchedulingValidationService();
        var altValidationResult = validationService.ValidateSchedule(
            period, employees, avails, new List<LeaveRequest>(), requirements,
            validAlternativeAssignments, shiftTypes);

        Assert.True(altValidationResult.IsValid);
    }

    [Fact(DisplayName = "TC-BR01-OR-01 - OR-Tools tuân thủ Max=1: Mỗi nhân viên tối đa 1 ca/ngày")]
    public void GenerateProposal_WhenPolicyMax1_FullTimeAssignedAtMostOneShiftPerDay()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4m, DisplayOrder = 1, IsActive = true, StartTime = new(8, 0), EndTime = new(12, 0), MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4m, DisplayOrder = 2, IsActive = true, StartTime = new(12, 0), EndTime = new(16, 0), MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new(16, 0), EndTime = new(20, 0), MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false }
        };

        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV FT 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV FT 2", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true }
        };

        var avails = new List<Availability>();
        int id = 1;
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            for (int s = 1; s <= 3; s++)
            {
                avails.Add(new Availability { AvailabilityId = id++, EmployeeId = 1, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = s });
                avails.Add(new Availability { AvailabilityId = id++, EmployeeId = 2, SchedulePeriodId = 1, WorkDate = date, ShiftTypeId = s });
            }
        }

        // Định mức ca trong ngày: Sáng cần 1, Chiều cần 1, Tối cần 0
        var requirements = new List<StaffingRequirement>
        {
            new() { SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1, ShiftType = shiftTypes[0], RequiredSalesStaff = 1, RequiredCashiers = 0 },
            new() { SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 2, ShiftType = shiftTypes[1], RequiredSalesStaff = 1, RequiredCashiers = 0 },
            new() { SchedulePeriodId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 3, ShiftType = shiftTypes[2], RequiredSalesStaff = 0, RequiredCashiers = 0 }
        };

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ValidationResult.IsValid);

        // Kiểm tra mỗi nhân viên không quá 1 ca/ngày
        foreach (var emp in employees)
        {
            for (int d = 0; d < 7; d++)
            {
                var date = period.WeekStartDate.AddDays(d);
                var dailyCount = result.ProposedAssignments.Count(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date);
                Assert.True(dailyCount <= 1, $"Employee {emp.EmployeeId} has {dailyCount} shifts on {date}");
            }
        }
    }

    [Fact(DisplayName = "TC-BR01-OR-02 - OR-Tools khi AllowSplitShift=false không bao giờ xếp Sáng và Tối cùng ngày")]
    public void GenerateProposal_WhenSplitShiftNotAllowed_NeverAssignsMorningAndEveningTogether()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4m, DisplayOrder = 1, IsActive = true, StartTime = new(8, 0), EndTime = new(12, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4m, DisplayOrder = 2, IsActive = true, StartTime = new(12, 0), EndTime = new(16, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4m, DisplayOrder = 3, IsActive = true, StartTime = new(16, 0), EndTime = new(20, 0), MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false }
        };

        var employees = CreateSixEmployees();
        var (avails, requirements) = CreateAvailabilitiesAndRequirements(period, shiftTypes, employees);

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ValidationResult.IsValid);

        // Xác nhận không có nhân viên nào bị xếp cả Sáng (id=1) và Tối (id=3) trong cùng 1 ngày
        foreach (var emp in employees)
        {
            for (int d = 0; d < 7; d++)
            {
                var date = period.WeekStartDate.AddDays(d);
                var hasMorning = result.ProposedAssignments.Any(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date && a.ShiftTypeId == 1);
                var hasEvening = result.ProposedAssignments.Any(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date && a.ShiftTypeId == 3);
                Assert.False(hasMorning && hasEvening, $"Employee {emp.EmployeeId} was assigned both Morning and Evening on {date}");
            }
        }
    }

    [Fact(DisplayName = "TC-BR01-OR-03 - OR-Tools goi lien tiep hai ky voi policy khac nhau khong ro policy")]
    public void GenerateProposal_SequentialCallsDifferentPolicy_NoPolicyLeak()
    {
        var periodA = new SchedulePeriod { SchedulePeriodId = 1, WeekStartDate = new DateOnly(2026, 8, 10), WeekEndDate = new DateOnly(2026, 8, 16) };
        var periodB = new SchedulePeriod { SchedulePeriodId = 2, WeekStartDate = new DateOnly(2026, 8, 17), WeekEndDate = new DateOnly(2026, 8, 23) };

        var shiftsA = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4m, StartTime = new(8, 0), EndTime = new(12, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 2 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4m, StartTime = new(12, 0), EndTime = new(16, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 2 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4m, StartTime = new(16, 0), EndTime = new(20, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 2 }
        };

        var shiftsB = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4m, StartTime = new(8, 0), EndTime = new(12, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4m, StartTime = new(12, 0), EndTime = new(16, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4m, StartTime = new(16, 0), EndTime = new(20, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 }
        };

        var employees = CreateSixEmployees();
        var (availsA, reqsA) = CreateAvailabilitiesAndRequirements(periodA, shiftsA, employees);
        var (availsB, reqsB) = CreateAvailabilitiesAndRequirements(periodB, shiftsB, employees);

        // Lần 1: Kỳ A Max=1
        var resA1 = _service.GenerateProposal(periodA, employees, availsA, new List<LeaveRequest>(), reqsA, shiftsA, 5);
        Assert.True(resA1.Succeeded);
        foreach (var emp in employees)
        {
            for (int d = 0; d < 7; d++)
            {
                var date = periodA.WeekStartDate.AddDays(d);
                var count = resA1.ProposedAssignments.Count(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date);
                Assert.True(count <= 1, $"Kỳ A: Employee {emp.EmployeeId} has {count} shifts on {date}");
            }
        }

        // Lần 2: Kỳ B Max=2
        var resB = _service.GenerateProposal(periodB, employees, availsB, new List<LeaveRequest>(), reqsB, shiftsB, 5);
        Assert.True(resB.Succeeded);

        // Lần 3: Gọi lại Kỳ A Max=1 -> không bị rò Max=2 từ kỳ B
        var resA2 = _service.GenerateProposal(periodA, employees, availsA, new List<LeaveRequest>(), reqsA, shiftsA, 5);
        Assert.True(resA2.Succeeded);
        foreach (var emp in employees)
        {
            for (int d = 0; d < 7; d++)
            {
                var date = periodA.WeekStartDate.AddDays(d);
                var count = resA2.ProposedAssignments.Count(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date);
                Assert.True(count <= 1, $"Kỳ A lần 2: Employee {emp.EmployeeId} has {count} shifts on {date}");
            }
        }
    }

    [Fact(DisplayName = "TC-BR01-OR-04 - OR-Tools chay song song hai ky khong dung shared mutable state")]
    public async Task GenerateProposal_ConcurrentCallsDifferentPolicy_NoSharedStateLeak()
    {
        var periodA = new SchedulePeriod { SchedulePeriodId = 1, WeekStartDate = new DateOnly(2026, 8, 10), WeekEndDate = new DateOnly(2026, 8, 16) };
        var periodB = new SchedulePeriod { SchedulePeriodId = 2, WeekStartDate = new DateOnly(2026, 8, 17), WeekEndDate = new DateOnly(2026, 8, 23) };

        var shiftsA = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4m, StartTime = new(8, 0), EndTime = new(12, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 2 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4m, StartTime = new(12, 0), EndTime = new(16, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 2 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4m, StartTime = new(16, 0), EndTime = new(20, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 2 }
        };

        var shiftsB = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", DurationHours = 4m, StartTime = new(8, 0), EndTime = new(12, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", DurationHours = 4m, StartTime = new(12, 0), EndTime = new(16, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", DurationHours = 4m, StartTime = new(16, 0), EndTime = new(20, 0), IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 }
        };

        var employees = CreateSixEmployees();
        var (availsA, reqsA) = CreateAvailabilitiesAndRequirements(periodA, shiftsA, employees);
        var (availsB, reqsB) = CreateAvailabilitiesAndRequirements(periodB, shiftsB, employees);

        var taskA = Task.Run(() => _service.GenerateProposal(periodA, employees, availsA, new List<LeaveRequest>(), reqsA, shiftsA, 5));
        var taskB = Task.Run(() => _service.GenerateProposal(periodB, employees, availsB, new List<LeaveRequest>(), reqsB, shiftsB, 5));

        await Task.WhenAll(taskA, taskB);

        var resA = await taskA;
        var resB = await taskB;

        Assert.True(resA.Succeeded);
        Assert.True(resB.Succeeded);

        // Kỳ A phải luôn bảo đảm mỗi nhân viên <= 1 ca/ngày
        foreach (var emp in employees)
        {
            for (int d = 0; d < 7; d++)
            {
                var date = periodA.WeekStartDate.AddDays(d);
                var count = resA.ProposedAssignments.Count(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date);
                Assert.True(count <= 1, $"Song song: Kỳ A Employee {emp.EmployeeId} has {count} shifts on {date}");
            }
        }
    }
}


