using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class OrToolsSchedulingServiceTests
{
    private readonly OrToolsSchedulingService _service = new(
        new ScheduleValidationService(),
        NullLogger<OrToolsSchedulingService>.Instance);

    [Fact(DisplayName = "TC06-01 - UC06 BR15 - OR-Tools CP-SAT sinh lịch đề xuất thành công khi đủ nhân sự và rảnh")]
    public void GenerateProposal_WhenSufficientEmployeesAndAvailabilities_ReturnsFeasibleProposal()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DisplayOrder = 1, IsActive = true },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DisplayOrder = 2, IsActive = true },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DisplayOrder = 3, IsActive = true }
        };

        var employees = new List<Employee>
        {
            new Employee { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true },
            new Employee { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV 2", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true },
            new Employee { EmployeeId = 3, EmployeeCode = "NV03", FullName = "NV 3", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true },
            new Employee { EmployeeId = 4, EmployeeCode = "NV04", FullName = "NV 4", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true },
            new Employee { EmployeeId = 5, EmployeeCode = "NV05", FullName = "NV 5", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true },
            new Employee { EmployeeId = 6, EmployeeCode = "NV06", FullName = "NV 6", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true }
        };

        var avails = new List<Availability>();
        var requirements = new List<StaffingRequirement>();

        // Đăng ký rảnh toàn bộ và định mức ca
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            foreach (var st in shiftTypes)
            {
                foreach (var emp in employees)
                {
                    avails.Add(new Availability { EmployeeId = emp.EmployeeId, WorkDate = date, ShiftTypeId = st.ShiftTypeId });
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

        var result = _service.GenerateProposal(period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

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

        var shiftTypes = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DisplayOrder = 1, IsActive = true },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DisplayOrder = 2, IsActive = true },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DisplayOrder = 3, IsActive = true }
        };

        var employees = new List<Employee>
        {
            new Employee { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV 1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, EmploymentType = EmploymentType.PartTime, IsActive = true }
        };

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement { WorkDate = period.WeekStartDate, ShiftTypeId = 1, RequiredSalesStaff = 5, RequiredCashiers = 5 }
        };

        var result = _service.GenerateProposal(period, employees, new List<Availability>(), new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.False(result.Succeeded);
        Assert.Equal("INFEASIBLE", result.Status.ToUpperInvariant());
    }
}
