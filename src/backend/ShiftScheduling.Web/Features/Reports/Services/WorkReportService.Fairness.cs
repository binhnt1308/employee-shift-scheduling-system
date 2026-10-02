using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

public sealed partial class WorkReportService
{
    private static WorkloadFairnessSummaryViewModel CalculateFairnessSummary(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<ShiftAssignment> publishedAssignments,
        IReadOnlyList<StaffingRequirement>? requirements = null)
    {
        // Lấy cả nhân viên có ca và nhân viên thuộc kỳ nhưng chưa được xếp ca để chênh lệch tải không bị "đẹp giả".
        var employeeMap = new Dictionary<int, Employee>();

        foreach (var assignment in publishedAssignments)
        {
            if (!employeeMap.ContainsKey(assignment.EmployeeId))
            {
                var emp = assignment.Employee ?? activeEmployees.FirstOrDefault(e => e.EmployeeId == assignment.EmployeeId);
                if (emp != null)
                {
                    employeeMap[assignment.EmployeeId] = emp;
                }
            }
        }

        foreach (var employee in activeEmployees)
        {
            if (employee.StartDate <= period.WeekEndDate && !employeeMap.ContainsKey(employee.EmployeeId))
            {
                employeeMap[employee.EmployeeId] = employee;
            }
        }

        var periodEmployees = employeeMap.Values.ToList();

        // Khi navigation ShiftType chưa được nạp, dùng thời lượng từ StaffingRequirement làm nguồn dự phòng.
        var shiftDurationMap = new Dictionary<int, decimal>();
        if (requirements != null)
        {
            foreach (var req in requirements)
            {
                if (req.ShiftType != null && !shiftDurationMap.ContainsKey(req.ShiftTypeId))
                {
                    shiftDurationMap[req.ShiftTypeId] = req.ShiftType.DurationHours;
                }
            }
        }

        var scheduledHoursByEmployee = publishedAssignments
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(a =>
                {
                    if (a.ShiftType != null)
                    {
                        return a.ShiftType.DurationHours;
                    }
                    if (shiftDurationMap.TryGetValue(a.ShiftTypeId, out var dur))
                    {
                        return dur;
                    }
                    return 0m;
                }));

        var ftEmployees = periodEmployees.Where(e => e.EmploymentType == EmploymentType.FullTime).ToList();
        var ptEmployees = periodEmployees.Where(e => e.EmploymentType == EmploymentType.PartTime).ToList();

        return new WorkloadFairnessSummaryViewModel
        {
            FullTime = BuildFairnessGroup(EmploymentType.FullTime, ftEmployees, scheduledHoursByEmployee),
            PartTime = BuildFairnessGroup(EmploymentType.PartTime, ptEmployees, scheduledHoursByEmployee)
        };
    }

    private static WorkloadFairnessGroupViewModel BuildFairnessGroup(
        EmploymentType employmentType,
        List<Employee> groupEmployees,
        Dictionary<int, decimal> scheduledHoursByEmployee)
    {
        if (groupEmployees.Count == 0)
        {
            return new WorkloadFairnessGroupViewModel
            {
                EmploymentType = employmentType,
                EmployeeCount = 0,
                MinimumScheduledHours = 0m,
                MaximumScheduledHours = 0m,
                SpreadHours = 0m
            };
        }

        var hoursList = groupEmployees
            .Select(e => scheduledHoursByEmployee.TryGetValue(e.EmployeeId, out var h) ? h : 0m)
            .ToList();

        decimal min = hoursList.Min();
        decimal max = hoursList.Max();
        decimal spread = max - min;

        return new WorkloadFairnessGroupViewModel
        {
            EmploymentType = employmentType,
            EmployeeCount = groupEmployees.Count,
            MinimumScheduledHours = min,
            MaximumScheduledHours = max,
            SpreadHours = spread
        };
    }
}
