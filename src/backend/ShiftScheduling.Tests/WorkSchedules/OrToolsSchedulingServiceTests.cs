using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
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

        // Quy tắc cứng: mỗi nhân viên chỉ được nhận tối đa một ca trong cùng ngày.
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

    [Fact(DisplayName = "TC-BR10-01: Hai nhân viên tương đương, người đăng ký mức ưu tiên được chọn")]
    public void GenerateProposal_WhenTwoEquivalentEmployees_PrefersHigherPreferenceLevel()
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
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV 2", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true }
        };

        var targetDate = period.WeekStartDate;
        var avails = new List<Availability>
        {
            new() { EmployeeId = 1, WorkDate = targetDate, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Preferred },
            new() { EmployeeId = 2, WorkDate = targetDate, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = morning.ShiftTypeId, ShiftType = morning, RequiredSalesStaff = d == 0 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = d == 0 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = afternoon.ShiftTypeId, ShiftType = afternoon, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = evening.ShiftTypeId, ShiftType = evening, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
        }

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded);
        Assert.Single(result.ProposedAssignments);
        var assigned = result.ProposedAssignments.Single();
        Assert.Equal(1, assigned.EmployeeId);
        Assert.Equal(morning.ShiftTypeId, assigned.ShiftTypeId);
        Assert.Equal(targetDate, assigned.WorkDate);
    }

    [Fact(DisplayName = "TC-BR10-02: Mức bình thường vẫn hợp lệ khi không có lựa chọn ưu tiên")]
    public void GenerateProposal_WhenOnlyNormalPreferenceAvailable_SucceedsNormally()
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
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV 2", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true }
        };

        var targetDate = period.WeekStartDate;
        var avails = new List<Availability>
        {
            new() { EmployeeId = 1, WorkDate = targetDate, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = targetDate, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = morning.ShiftTypeId, ShiftType = morning, RequiredSalesStaff = d == 0 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = d == 0 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = afternoon.ShiftTypeId, ShiftType = afternoon, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = evening.ShiftTypeId, ShiftType = evening, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
        }

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded);
        Assert.Single(result.ProposedAssignments);
        Assert.True(result.ValidationResult.IsValid);
    }

    [Fact(DisplayName = "TC-BR10-03: Solver tránh hai ca EVENING liên tiếp khi có phương án thay thế tương đương")]
    public void GenerateProposal_WhenAlternativeExists_AvoidsConsecutiveEveningShifts()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        // Chính sách BR-01 là tối đa 1 ca/ngày, các ca có cùng thời lượng (4h)
        var shiftTypes = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4m, DisplayOrder = 1, IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DurationHours = 4m, DisplayOrder = 2, IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4m, DisplayOrder = 3, IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 1 }
        };

        var morning = shiftTypes[0];
        var afternoon = shiftTypes[1];
        var evening = shiftTypes[2];

        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV 2", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true }
        };

        var day0 = period.WeekStartDate;
        var day1 = period.WeekStartDate.AddDays(1);

        // Cả hai nhân viên đều rảnh cả MORNING và EVENING trong hai ngày, tất cả mức Normal
        var avails = new List<Availability>
        {
            new() { EmployeeId = 1, WorkDate = day0, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 1, WorkDate = day0, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 1, WorkDate = day1, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 1, WorkDate = day1, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },

            new() { EmployeeId = 2, WorkDate = day0, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day0, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day1, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day1, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        // Mỗi ngày cần: 1 người ca MORNING, 1 người ca EVENING
        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            bool isDay0Or1 = d == 0 || d == 1;
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = morning.ShiftTypeId, ShiftType = morning, RequiredSalesStaff = isDay0Or1 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = isDay0Or1 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = afternoon.ShiftTypeId, ShiftType = afternoon, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = evening.ShiftTypeId, ShiftType = evening, RequiredSalesStaff = isDay0Or1 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = isDay0Or1 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
        }

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        // Assertion bắt buộc:
        // 1. Solver thành công
        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ValidationResult.IsValid);

        // 2. Có đúng 4 assignments
        Assert.Equal(4, result.ProposedAssignments.Count);

        // 3. Mỗi nhân viên có đúng 2 assignments
        Assert.Equal(2, result.ProposedAssignments.Count(a => a.EmployeeId == 1));
        Assert.Equal(2, result.ProposedAssignments.Count(a => a.EmployeeId == 2));

        // 4. Mỗi nhân viên có tổng phút bằng nhau
        var nv1Minutes = result.ProposedAssignments
            .Where(a => a.EmployeeId == 1)
            .Sum(a => shiftTypes.First(s => s.ShiftTypeId == a.ShiftTypeId).DurationHours * 60m);
        var nv2Minutes = result.ProposedAssignments
            .Where(a => a.EmployeeId == 2)
            .Sum(a => shiftTypes.First(s => s.ShiftTypeId == a.ShiftTypeId).DurationHours * 60m);
        Assert.Equal(nv1Minutes, nv2Minutes);

        // 5. Không nhân viên nào có EVENING trong cả hai ngày liên tiếp
        bool nv1HasConsecEvening = result.ProposedAssignments.Any(a => a.EmployeeId == 1 && a.WorkDate == day0 && a.ShiftTypeId == evening.ShiftTypeId)
            && result.ProposedAssignments.Any(a => a.EmployeeId == 1 && a.WorkDate == day1 && a.ShiftTypeId == evening.ShiftTypeId);
        bool nv2HasConsecEvening = result.ProposedAssignments.Any(a => a.EmployeeId == 2 && a.WorkDate == day0 && a.ShiftTypeId == evening.ShiftTypeId)
            && result.ProposedAssignments.Any(a => a.EmployeeId == 2 && a.WorkDate == day1 && a.ShiftTypeId == evening.ShiftTypeId);

        Assert.False(nv1HasConsecEvening, "NV1 không được có ca EVENING trong cả hai ngày liên tiếp");
        Assert.False(nv2HasConsecEvening, "NV2 không được có ca EVENING trong cả hai ngày liên tiếp");
    }

    [Fact(DisplayName = "TC-BR10-04: Nếu ca tối liên tiếp là bắt buộc để đủ định mức, solver vẫn thành công")]
    public void GenerateProposal_WhenConsecutiveEveningIsUnavoidable_StillSucceedsFeasibly()
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
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true }
        };

        var day0 = period.WeekStartDate;
        var day1 = period.WeekStartDate.AddDays(1);

        // Chỉ có NV1 khả thi cho cả hai ca tối liên tiếp
        var avails = new List<Availability>
        {
            new() { EmployeeId = 1, WorkDate = day0, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 1, WorkDate = day1, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = morning.ShiftTypeId, ShiftType = morning, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = afternoon.ShiftTypeId, ShiftType = afternoon, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = evening.ShiftTypeId, ShiftType = evening, RequiredSalesStaff = (d == 0 || d == 1) ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = (d == 0 || d == 1) ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
        }

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.ProposedAssignments.Count);
        Assert.All(result.ProposedAssignments, a => Assert.Equal(1, a.EmployeeId));
        Assert.True(result.ValidationResult.IsValid);
    }

    [Fact(DisplayName = "TC-BR10-05: Hai ca có thời lượng khác nhau, objective cân bằng theo phút/giờ thực tế")]
    public void GenerateProposal_WhenDifferentShiftDurations_BalancesActualWorkloadMinutes()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        // Ba ca với thời lượng khác nhau: Sáng = 6h (360m), Chiều = 2h (120m), Tối = 4h (240m)
        var shiftTypes = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng dài", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(12, 0), DurationHours = 6m, DisplayOrder = 1, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = true, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều ngắn", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(14, 0), DurationHours = 2m, DisplayOrder = 2, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = true, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối vừa", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4m, DisplayOrder = 3, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = true, ConfigurationVersion = 1 }
        };

        var morning = shiftTypes[0];
        var afternoon = shiftTypes[1];
        var evening = shiftTypes[2];

        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV 2", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true }
        };

        var day0 = period.WeekStartDate;
        var day1 = period.WeekStartDate.AddDays(1);

        // Ngày 0 cần 1 ca sáng (6h) và 1 ca chiều (2h)
        // Ngày 1 cần 1 ca tối (4h)
        // Tổng thời lượng: 6h + 2h + 4h = 12h.
        // Nếu cân bằng theo phút thực tế: một người 6h (1 ca sáng), một người 2h + 4h = 6h (2 ca). Chênh lệch phút = 0!
        // Nếu chỉ đếm số ca (kiểu cũ): 1 người 2 ca (ví dụ 6h + 4h = 10h), 1 người 1 ca (2h) -> chênh lệch 8h!
        var avails = new List<Availability>
        {
            new() { EmployeeId = 1, WorkDate = day0, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 1, WorkDate = day0, ShiftTypeId = afternoon.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 1, WorkDate = day1, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day0, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day0, ShiftTypeId = afternoon.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day1, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = morning.ShiftTypeId, ShiftType = morning, RequiredSalesStaff = d == 0 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = d == 0 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = afternoon.ShiftTypeId, ShiftType = afternoon, RequiredSalesStaff = d == 0 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = d == 0 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = evening.ShiftTypeId, ShiftType = evening, RequiredSalesStaff = d == 1 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = d == 1 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
        }

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.ProposedAssignments.Count);

        var nv1Minutes = result.ProposedAssignments
            .Where(a => a.EmployeeId == 1)
            .Sum(a => shiftTypes.First(s => s.ShiftTypeId == a.ShiftTypeId).DurationHours * 60m);
        var nv2Minutes = result.ProposedAssignments
            .Where(a => a.EmployeeId == 2)
            .Sum(a => shiftTypes.First(s => s.ShiftTypeId == a.ShiftTypeId).DurationHours * 60m);

        // Cả 2 nhân viên đều được phân bổ đúng 360 phút (6 giờ), đạt độ cân bằng giờ công tuyệt đối
        Assert.Equal(360m, nv1Minutes);
        Assert.Equal(360m, nv2Minutes);
    }

    [Fact(DisplayName = "TC-BR10-06: Mức ưu tiên Preferred được ưu tiên cao hơn tránh ca tối liên tiếp")]
    public void GenerateProposal_WhenPreferenceConflictsWithConsecutiveEvening_PrefersPreferenceLevel()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4m, DisplayOrder = 1, IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DurationHours = 4m, DisplayOrder = 2, IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4m, DisplayOrder = 3, IsActive = true, MaxShiftsPerEmployeePerDay = 1, AllowSplitShift = false, ConfigurationVersion = 1 }
        };

        var morning = shiftTypes[0];
        var afternoon = shiftTypes[1];
        var evening = shiftTypes[2];

        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV 2", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true }
        };

        var day0 = period.WeekStartDate;
        var day1 = period.WeekStartDate.AddDays(1);

        // Ngày 0: NV1 chỉ rảnh Evening (Normal), NV2 chỉ rảnh Morning (Normal) -> Day 0 cố định: NV1 làm Evening, NV2 làm Morning
        // Ngày 1: Cả hai đều có thể làm Morning (Normal) hoặc Evening. Nhưng Evening của NV1 là Preferred, của NV2 là Normal.
        var avails = new List<Availability>
        {
            new() { EmployeeId = 1, WorkDate = day0, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day0, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },

            new() { EmployeeId = 1, WorkDate = day1, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 1, WorkDate = day1, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Preferred },
            new() { EmployeeId = 2, WorkDate = day1, ShiftTypeId = morning.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = 2, WorkDate = day1, ShiftTypeId = evening.ShiftTypeId, PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        // Mỗi ngày cần 1 ca MORNING và 1 ca EVENING (tổng 4 ca, mỗi người 2 ca = 8h -> tổng phút cân bằng tuyệt đối)
        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            bool isDay0Or1 = d == 0 || d == 1;
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = morning.ShiftTypeId, ShiftType = morning, RequiredSalesStaff = isDay0Or1 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = isDay0Or1 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = afternoon.ShiftTypeId, ShiftType = afternoon, RequiredSalesStaff = 0, RequiredCashiers = 0, RequiredExperiencedStaff = 0, MinimumLevel = EmployeeLevel.Level2 });
            requirements.Add(new StaffingRequirement { WorkDate = date, ShiftTypeId = evening.ShiftTypeId, ShiftType = evening, RequiredSalesStaff = isDay0Or1 ? 1 : 0, RequiredCashiers = 0, RequiredExperiencedStaff = isDay0Or1 ? 1 : 0, MinimumLevel = EmployeeLevel.Level2 });
        }

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ValidationResult.IsValid);
        Assert.Equal(4, result.ProposedAssignments.Count);
        Assert.Equal(2, result.ProposedAssignments.Count(a => a.EmployeeId == 1));
        Assert.Equal(2, result.ProposedAssignments.Count(a => a.EmployeeId == 2));

        var nv1Minutes = result.ProposedAssignments
            .Where(a => a.EmployeeId == 1)
            .Sum(a => shiftTypes.First(s => s.ShiftTypeId == a.ShiftTypeId).DurationHours * 60m);
        var nv2Minutes = result.ProposedAssignments
            .Where(a => a.EmployeeId == 2)
            .Sum(a => shiftTypes.First(s => s.ShiftTypeId == a.ShiftTypeId).DurationHours * 60m);
        Assert.Equal(nv1Minutes, nv2Minutes);

        // Phương án A: NV1 nhận ca Preferred (Evening Day 1) mặc dù phải làm Evening Day 0 (tạo 1 cặp tối liên tiếp).
        // Phương án B: Tránh tối liên tiếp (NV1 làm Morning Day 1, NV2 làm Evening Day 1) nhưng mất ca Preferred (dùng Normal).
        // Vì Preference > Ca tối liên tiếp, solver bắt buộc phải chọn Phương án A!
        var nv1Day1Evening = result.ProposedAssignments.Any(a => a.EmployeeId == 1 && a.WorkDate == day1 && a.ShiftTypeId == evening.ShiftTypeId);
        Assert.True(nv1Day1Evening, "Solver phải chọn phương án dùng ca Preferred của NV1");

        var nv1Day0Evening = result.ProposedAssignments.Any(a => a.EmployeeId == 1 && a.WorkDate == day0 && a.ShiftTypeId == evening.ShiftTypeId);
        Assert.True(nv1Day0Evening, "NV1 làm ca tối ngày 0 và ngày 1 (chấp nhận tối liên tiếp để thỏa Preferred)");
    }

    [Fact(DisplayName = "TC-ORT-HISTORY-01: Kỳ lịch dùng 3 ca lịch sử IsActive=false giải thành công, dùng đúng ShiftTypeId cũ")]
    public void GenerateProposal_WhenHistoricalPeriodWithInactiveShifts_SolvesSuccessfully()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var historyShifts = new List<ShiftType>
        {
            new()
            {
                ShiftTypeId = 7,
                ShiftCode = "MORNING",
                ShiftName = "Ca sáng (v1)",
                DurationHours = 4m,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                DisplayOrder = 1,
                IsActive = false,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            },
            new()
            {
                ShiftTypeId = 8,
                ShiftCode = "AFTERNOON",
                ShiftName = "Ca chiều (v1)",
                DurationHours = 4m,
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(16, 0),
                DisplayOrder = 2,
                IsActive = false,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            },
            new()
            {
                ShiftTypeId = 9,
                ShiftCode = "EVENING",
                ShiftName = "Ca tối (v1)",
                DurationHours = 4m,
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(20, 0),
                DisplayOrder = 3,
                IsActive = false,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            }
        };

        var employees = CreateSixEmployees();
        var (avails, requirements) = CreateAvailabilitiesAndRequirements(period, historyShifts, employees);

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, historyShifts, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ValidationResult.IsValid);
        Assert.NotEmpty(result.ProposedAssignments);
        Assert.All(result.ProposedAssignments, a => Assert.Contains(a.ShiftTypeId, new[] { 7, 8, 9 }));
        Assert.DoesNotContain(result.ProposedAssignments, a => a.ShiftTypeId is not (7 or 8 or 9));
    }

    [Fact(DisplayName = "TC-ORT-HISTORY-02: Đầu vào chứa cả ca lịch sử và ca active -> Fallback chọn đúng 3 ca active")]
    public void GenerateProposal_WhenInputHasMultipleVersions_FallsBackToActiveShifts()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var historyShifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 7, ShiftCode = "MORNING", ShiftName = "Ca sáng (v1)", DurationHours = 4m, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DisplayOrder = 1, IsActive = false, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 8, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều (v1)", DurationHours = 4m, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DisplayOrder = 2, IsActive = false, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 },
            new() { ShiftTypeId = 9, ShiftCode = "EVENING", ShiftName = "Ca tối (v1)", DurationHours = 4m, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DisplayOrder = 3, IsActive = false, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 1 }
        };

        var activeShifts = new List<ShiftType>
        {
            new() { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng (v2)", DurationHours = 4m, StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DisplayOrder = 1, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 2 },
            new() { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều (v2)", DurationHours = 4m, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DisplayOrder = 2, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 2 },
            new() { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối (v2)", DurationHours = 4m, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DisplayOrder = 3, IsActive = true, MaxShiftsPerEmployeePerDay = 2, AllowSplitShift = false, ConfigurationVersion = 2 }
        };

        var allShifts = historyShifts.Concat(activeShifts).ToList();

        var employees = CreateSixEmployees();
        var (avails, requirements) = CreateAvailabilitiesAndRequirements(period, activeShifts, employees);

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, allShifts, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ValidationResult.IsValid);
        Assert.NotEmpty(result.ProposedAssignments);
        Assert.All(result.ProposedAssignments, a => Assert.Contains(a.ShiftTypeId, new[] { 1, 2, 3 }));
        Assert.DoesNotContain(result.ProposedAssignments, a => a.ShiftTypeId is 7 or 8 or 9);
    }

    [Fact(DisplayName = "TC-ORT-BR20-01: Nghỉ giữa ca Tối ngày D và ca Sáng ngày D+1 < 12h phải được ràng buộc, không vi phạm BR20")]
    public void GenerateProposal_WhenConsecutiveEveningAndMorningRestLessThan12Hours_SatisfiesBR20()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = new List<ShiftType>
        {
            new()
            {
                ShiftTypeId = 1,
                ShiftCode = "MORNING",
                ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                DurationHours = 4m,
                DisplayOrder = 1,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            },
            new()
            {
                ShiftTypeId = 2,
                ShiftCode = "AFTERNOON",
                ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(16, 0),
                DurationHours = 4m,
                DisplayOrder = 2,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            },
            new()
            {
                ShiftTypeId = 3,
                ShiftCode = "EVENING",
                ShiftName = "Ca tối",
                StartTime = new TimeOnly(17, 0),
                EndTime = new TimeOnly(21, 0),
                DurationHours = 4m,
                DisplayOrder = 3,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            }
        };

        var ftEmp = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "Sales FT",
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level2,
            EmploymentType = EmploymentType.FullTime,
            IsActive = true
        };
        var ptEmp = new Employee
        {
            EmployeeId = 2,
            EmployeeCode = "NV02",
            FullName = "Sales PT",
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level2,
            EmploymentType = EmploymentType.PartTime,
            IsActive = true
        };
        var employees = new List<Employee> { ftEmp, ptEmp };

        var dayD = period.WeekStartDate;
        var dayDPlus1 = period.WeekStartDate.AddDays(1);

        var avails = new List<Availability>
        {
            new() { EmployeeId = ftEmp.EmployeeId, WorkDate = dayD, ShiftTypeId = 3, PreferenceLevel = AvailabilityPreferenceLevel.Preferred },
            new() { EmployeeId = ftEmp.EmployeeId, WorkDate = dayDPlus1, ShiftTypeId = 1, PreferenceLevel = AvailabilityPreferenceLevel.Preferred },
            new() { EmployeeId = ptEmp.EmployeeId, WorkDate = dayD, ShiftTypeId = 3, PreferenceLevel = AvailabilityPreferenceLevel.Normal },
            new() { EmployeeId = ptEmp.EmployeeId, WorkDate = dayDPlus1, ShiftTypeId = 1, PreferenceLevel = AvailabilityPreferenceLevel.Normal }
        };

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            foreach (var st in shiftTypes)
            {
                bool isEveningDayD = date == dayD && st.ShiftTypeId == 3;
                bool isMorningDayDPlus1 = date == dayDPlus1 && st.ShiftTypeId == 1;

                requirements.Add(new StaffingRequirement
                {
                    WorkDate = date,
                    ShiftTypeId = st.ShiftTypeId,
                    ShiftType = st,
                    RequiredSalesStaff = (isEveningDayD || isMorningDayDPlus1) ? 1 : 0,
                    RequiredCashiers = 0,
                    RequiredExperiencedStaff = 0,
                    MinimumLevel = EmployeeLevel.Level1
                });
            }
        }

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.ProposedAssignments.Count);
        Assert.True(result.ValidationResult.IsValid);
        Assert.DoesNotContain(result.ValidationResult.HardErrors, e => e.Contains("BR20"));

        foreach (var emp in employees)
        {
            var hasEvening = result.ProposedAssignments.Any(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == dayD && a.ShiftTypeId == 3);
            var hasMorningNext = result.ProposedAssignments.Any(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == dayDPlus1 && a.ShiftTypeId == 1);
            Assert.False(hasEvening && hasMorningNext, $"Nhân viên {emp.FullName} vi phạm BR20: làm cả ca tối ngày D và ca sáng ngày D+1");
        }
    }

    [Fact(DisplayName = "TC-ORT-BR20-02: 8 nhân viên rảnh toàn bộ tuần, mỗi ca 1 Sales 1 Cashier thỏa mãn BR20 và validation")]
    public void GenerateProposal_When8EmployeesAvailableAllWeek_GeneratesValidScheduleSatisfyingBR20()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = new List<ShiftType>
        {
            new()
            {
                ShiftTypeId = 1,
                ShiftCode = "MORNING",
                ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                DurationHours = 4m,
                DisplayOrder = 1,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            },
            new()
            {
                ShiftTypeId = 2,
                ShiftCode = "AFTERNOON",
                ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(16, 0),
                DurationHours = 4m,
                DisplayOrder = 2,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            },
            new()
            {
                ShiftTypeId = 3,
                ShiftCode = "EVENING",
                ShiftName = "Ca tối",
                StartTime = new TimeOnly(17, 0),
                EndTime = new TimeOnly(21, 0),
                DurationHours = 4m,
                DisplayOrder = 3,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false,
                ConfigurationVersion = 1
            }
        };

        var employees = new List<Employee>
        {
            new() { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV01", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV02", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 3, EmployeeCode = "NV03", FullName = "NV03", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level3, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 4, EmployeeCode = "NV04", FullName = "NV04", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 5, EmployeeCode = "NV05", FullName = "NV05", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true },
            new() { EmployeeId = 6, EmployeeCode = "NV06", FullName = "NV06", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level1, EmploymentType = EmploymentType.PartTime, IsActive = true },
            new() { EmployeeId = 7, EmployeeCode = "NV07", FullName = "NV07", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level3, EmploymentType = EmploymentType.FullTime, IsActive = true },
            new() { EmployeeId = 8, EmployeeCode = "NV08", FullName = "NV08", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level1, EmploymentType = EmploymentType.PartTime, IsActive = true }
        };

        var avails = new List<Availability>();
        foreach (var emp in employees)
        {
            for (int d = 0; d < 7; d++)
            {
                var date = period.WeekStartDate.AddDays(d);
                foreach (var st in shiftTypes)
                {
                    avails.Add(new Availability
                    {
                        EmployeeId = emp.EmployeeId,
                        WorkDate = date,
                        ShiftTypeId = st.ShiftTypeId,
                        PreferenceLevel = AvailabilityPreferenceLevel.Normal
                    });
                }
            }
        }

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            foreach (var st in shiftTypes)
            {
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

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 10);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(42, result.ProposedAssignments.Count);

        var empMap = employees.ToDictionary(e => e.EmployeeId);
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            foreach (var st in shiftTypes)
            {
                var shiftAssignments = result.ProposedAssignments
                    .Where(a => a.WorkDate == date && a.ShiftTypeId == st.ShiftTypeId)
                    .ToList();
                Assert.Equal(2, shiftAssignments.Count);

                var salesCount = shiftAssignments.Count(a => empMap[a.EmployeeId].Position == EmployeePosition.Sales);
                var cashierCount = shiftAssignments.Count(a => empMap[a.EmployeeId].Position == EmployeePosition.Cashier);
                var lvl2PlusCount = shiftAssignments.Count(a => empMap[a.EmployeeId].Level >= EmployeeLevel.Level2);

                Assert.Equal(1, salesCount);
                Assert.Equal(1, cashierCount);
                Assert.True(lvl2PlusCount >= 1);
            }
        }

        foreach (var emp in employees.Where(e => e.EmploymentType == EmploymentType.PartTime))
        {
            for (int d = 0; d < 7; d++)
            {
                var date = period.WeekStartDate.AddDays(d);
                var dayAssignments = result.ProposedAssignments
                    .Count(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date);
                Assert.True(dayAssignments <= 1, $"PT {emp.EmployeeCode} có nhiều hơn 1 ca trong ngày {date}");
            }
        }

        foreach (var emp in employees.Where(e => e.EmploymentType == EmploymentType.FullTime))
        {
            for (int d = 0; d < 7; d++)
            {
                var date = period.WeekStartDate.AddDays(d);
                var dayAssignments = result.ProposedAssignments
                    .Count(a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date);
                Assert.True(dayAssignments <= 2, $"FT {emp.EmployeeCode} vượt 2 ca/ngày trong ngày {date}");
            }
        }

        var shiftMap = shiftTypes.ToDictionary(s => s.ShiftTypeId);
        foreach (var emp in employees)
        {
            var empAssignments = result.ProposedAssignments
                .Where(a => a.EmployeeId == emp.EmployeeId)
                .OrderBy(a => a.WorkDate)
                .ThenBy(a => shiftMap[a.ShiftTypeId].StartTime)
                .ToList();

            for (int i = 0; i < empAssignments.Count; i++)
            {
                var cur = empAssignments[i];
                var curShift = shiftMap[cur.ShiftTypeId];
                var curEnd = cur.WorkDate.ToDateTime(curShift.EndTime);
                if (curShift.EndTime <= curShift.StartTime)
                {
                    curEnd = curEnd.AddDays(1);
                }

                for (int j = i + 1; j < empAssignments.Count; j++)
                {
                    var next = empAssignments[j];
                    if (next.WorkDate == cur.WorkDate.AddDays(1))
                    {
                        var nextShift = shiftMap[next.ShiftTypeId];
                        var nextStart = next.WorkDate.ToDateTime(nextShift.StartTime);
                        var rest = (nextStart - curEnd).TotalHours;
                        Assert.True(rest >= SchedulingRuleConstants.MinimumDailyRestHours,
                            $"Nhân viên {emp.EmployeeCode} vi phạm nghỉ 12h: ca ngày {cur.WorkDate} kết thúc {curEnd:HH:mm}, ca ngày {next.WorkDate} bắt đầu {nextStart:HH:mm}, khoảng nghỉ {rest}h < 12h");
                    }
                }
            }
        }

        Assert.True(result.ValidationResult.IsValid);
        Assert.Empty(result.ValidationResult.HardErrors);
    }

    [Fact(DisplayName = "TC06-POS-01 - ProposedAssignmentItemViewModel ánh xạ đúng PositionLabel nghiệp vụ và JSON contract")]
    public void ProposedAssignmentItemViewModel_PositionLabel_MapsCorrectBusinessNamesAndJsonContract()
    {
        var salesItem = new ProposedAssignmentItemViewModel
        {
            Position = EmployeePosition.Sales
        };

        var cashierItem = new ProposedAssignmentItemViewModel
        {
            Position = EmployeePosition.Cashier
        };

        // Nhãn vị trí phải lấy từ hồ sơ nhân viên, không suy ra từ ca được xếp.
        Assert.Equal("Tư vấn", salesItem.PositionLabel);
        Assert.NotEqual("1", salesItem.PositionLabel);

        Assert.Equal("Thu ngân", cashierItem.PositionLabel);
        Assert.NotEqual("2", cashierItem.PositionLabel);

        // Kiểm tra JSON serialization contract cho API response (camelCase theo ASP.NET Core)
        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var salesJson = System.Text.Json.JsonSerializer.Serialize(salesItem, jsonOptions);
        var cashierJson = System.Text.Json.JsonSerializer.Serialize(cashierItem, jsonOptions);

        Assert.Contains("\"positionLabel\":\"Tư vấn\"", salesJson);
        Assert.Contains("\"positionLabel\":\"Thu ngân\"", cashierJson);

        using var salesDoc = System.Text.Json.JsonDocument.Parse(salesJson);
        Assert.Equal("Tư vấn", salesDoc.RootElement.GetProperty("positionLabel").GetString());

        using var cashierDoc = System.Text.Json.JsonDocument.Parse(cashierJson);
        Assert.Equal("Thu ngân", cashierDoc.RootElement.GetProperty("positionLabel").GetString());
    }
}


