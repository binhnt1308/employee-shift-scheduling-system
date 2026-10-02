using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;
using Xunit;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class OrToolsFullTimeConsecutivePreferenceTests
{
    private readonly OrToolsSchedulingService _service = new(
        new SchedulingValidationService(),
        NullLogger<OrToolsSchedulingService>.Instance);

    [Fact(DisplayName = "TC-ORT-FT-01: OR-Tools ưu tiên ghép ca liên tiếp cho FT")]
    public void GenerateProposal_WhenFullTimeHasMultipleFeasibleOptions_PrefersConsecutiveSameDayPairs()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = CreateThreeFixedShifts();
        var morning = shiftTypes[0];
        var afternoon = shiftTypes[1];
        var evening = shiftTypes[2];

        var employees = new List<Employee>
        {
            new()
            {
                EmployeeId = 1, EmployeeCode = "FT01", FullName = "NV FT 1",
                Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.FullTime, IsActive = true
            },
            new()
            {
                EmployeeId = 2, EmployeeCode = "FT02", FullName = "NV FT 2",
                Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.FullTime, IsActive = true
            }
        };

        var avails = new List<Availability>();
        for (int d = 0; d < 4; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            foreach (var emp in employees)
            {
                avails.Add(new Availability
                {
                    EmployeeId = emp.EmployeeId,
                    WorkDate = date,
                    ShiftTypeId = morning.ShiftTypeId,
                    PreferenceLevel = AvailabilityPreferenceLevel.Normal
                });
                avails.Add(new Availability
                {
                    EmployeeId = emp.EmployeeId,
                    WorkDate = date,
                    ShiftTypeId = afternoon.ShiftTypeId,
                    PreferenceLevel = AvailabilityPreferenceLevel.Normal
                });
            }
        }

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            bool isWorkDay = d < 4;
            requirements.Add(new StaffingRequirement
            {
                WorkDate = date,
                ShiftTypeId = morning.ShiftTypeId,
                ShiftType = morning,
                RequiredSalesStaff = isWorkDay ? 1 : 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = isWorkDay ? 1 : 0,
                MinimumLevel = EmployeeLevel.Level2
            });
            requirements.Add(new StaffingRequirement
            {
                WorkDate = date,
                ShiftTypeId = afternoon.ShiftTypeId,
                ShiftType = afternoon,
                RequiredSalesStaff = isWorkDay ? 1 : 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = isWorkDay ? 1 : 0,
                MinimumLevel = EmployeeLevel.Level2
            });
            requirements.Add(new StaffingRequirement
            {
                WorkDate = date,
                ShiftTypeId = evening.ShiftTypeId,
                ShiftType = evening,
                RequiredSalesStaff = 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 0,
                MinimumLevel = EmployeeLevel.Level2
            });
        }

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ValidationResult.IsValid);
        Assert.Equal(8, result.ProposedAssignments.Count);

        int consecutivePairsCount = 0;
        foreach (var emp in employees)
        {
            for (int d = 0; d < 7; d++)
            {
                var date = period.WeekStartDate.AddDays(d);
                var hasMorning = result.ProposedAssignments.Any(
                    a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date &&
                         a.ShiftTypeId == morning.ShiftTypeId);
                var hasAfternoon = result.ProposedAssignments.Any(
                    a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date &&
                         a.ShiftTypeId == afternoon.ShiftTypeId);
                var hasEvening = result.ProposedAssignments.Any(
                    a => a.EmployeeId == emp.EmployeeId && a.WorkDate == date &&
                         a.ShiftTypeId == evening.ShiftTypeId);

                if ((hasMorning && hasAfternoon) || (hasAfternoon && hasEvening))
                {
                    consecutivePairsCount++;
                }
            }
        }

        Assert.True(consecutivePairsCount >= 2, $"Số cặp ca đạt {consecutivePairsCount}, kỳ vọng >= 2.");
        Assert.Equal(4, consecutivePairsCount);
    }

    [Fact(DisplayName = "TC-ORT-FT-02: Ca đơn lẻ không thể ghép, solver vẫn thành công")]
    public void GenerateProposal_WhenOnlySingleShiftsAvailable_SucceedsWithoutFailure()
    {
        var period = new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };

        var shiftTypes = CreateThreeFixedShifts();
        var morning = shiftTypes[0];
        var afternoon = shiftTypes[1];
        var evening = shiftTypes[2];

        var employees = new List<Employee>
        {
            new()
            {
                EmployeeId = 1, EmployeeCode = "FT01", FullName = "NV FT 1",
                Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2,
                EmploymentType = EmploymentType.FullTime, IsActive = true
            }
        };

        var targetDate = period.WeekStartDate;
        var avails = new List<Availability>
        {
            new()
            {
                EmployeeId = 1, WorkDate = targetDate, ShiftTypeId = morning.ShiftTypeId,
                PreferenceLevel = AvailabilityPreferenceLevel.Normal
            }
        };

        var requirements = new List<StaffingRequirement>();
        for (int d = 0; d < 7; d++)
        {
            var date = period.WeekStartDate.AddDays(d);
            requirements.Add(new StaffingRequirement
            {
                WorkDate = date,
                ShiftTypeId = morning.ShiftTypeId,
                ShiftType = morning,
                RequiredSalesStaff = d == 0 ? 1 : 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = d == 0 ? 1 : 0,
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
                RequiredSalesStaff = 0,
                RequiredCashiers = 0,
                RequiredExperiencedStaff = 0,
                MinimumLevel = EmployeeLevel.Level2
            });
        }

        var result = _service.GenerateProposal(
            period, employees, avails, new List<LeaveRequest>(), requirements, shiftTypes, 5);

        Assert.True(result.Succeeded, result.Message);
        Assert.Single(result.ProposedAssignments);
        Assert.True(result.ValidationResult.IsValid);
    }

    private static List<ShiftType> CreateThreeFixedShifts() => new()
    {
        new()
        {
            ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng",
            StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0), DurationHours = 4m,
            DisplayOrder = 1, IsActive = true, MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false, ConfigurationVersion = 1
        },
        new()
        {
            ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều",
            StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(16, 0), DurationHours = 4m,
            DisplayOrder = 2, IsActive = true, MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false, ConfigurationVersion = 1
        },
        new()
        {
            ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối",
            StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(20, 0), DurationHours = 4m,
            DisplayOrder = 3, IsActive = true, MaxShiftsPerEmployeePerDay = 2,
            AllowSplitShift = false, ConfigurationVersion = 1
        }
    };
}
