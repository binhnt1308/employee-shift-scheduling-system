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
                Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.PartTime, IsActive = true
            }
        };

        var (avails, _) = CreateAvailabilitiesAndRequirements(period, shiftTypes, employees);

        var result = _service.GenerateProposal(
            period, employees, avails,
            new List<LeaveRequest>(), new List<StaffingRequirement>(),
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
                DisplayOrder = 1, IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0),
                DurationHours = 4m,
                DisplayOrder = 2, IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0),
                DurationHours = 4m,
                DisplayOrder = 3, IsActive = true
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
}


