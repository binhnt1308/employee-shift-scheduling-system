using ShiftScheduling.Common.Constants;
using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;
using Xunit;

namespace ShiftScheduling.Tests.Employees;

public sealed class EmployeeWeeklyHoursDisplayTests
{
    [Fact(DisplayName = "TC-NC01-021 - Part-time employee list item view model calculates expected target 20h and cap from constants")]
    public void PartTimeEmployeeListItemViewModel_CalculatesExpectedWeeklyHoursAndMaxWeeklyHours()
    {
        var model = new EmployeeListItemViewModel
        {
            EmployeeId = 5,
            EmployeeCode = "NV05",
            FullName = "Test PT Employee",
            EmploymentType = EmploymentType.PartTime,
            Position = EmployeePosition.Cashier,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };

        Assert.Equal(20, model.ExpectedWeeklyHours);
        Assert.Equal(SchedulingRuleConstants.PartTimeMaxWeeklyWorkHours, model.MaxWeeklyHours);
    }

    [Fact(DisplayName = "TC-NC01-022 - Full-time employee list item view model calculates expected target 48h and cap from constants")]
    public void FullTimeEmployeeListItemViewModel_CalculatesExpectedWeeklyHoursAndMaxWeeklyHours()
    {
        var model = new EmployeeListItemViewModel
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "Test FT Employee",
            EmploymentType = EmploymentType.FullTime,
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level3,
            IsActive = true
        };

        Assert.Equal(48, model.ExpectedWeeklyHours);
        Assert.Equal(SchedulingRuleConstants.FullTimeMaxWeeklyWorkHours, model.MaxWeeklyHours);
    }
}
