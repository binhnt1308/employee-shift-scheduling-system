using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

public sealed partial class WorkReportService
{
    private static ShiftCoverageSummaryViewModel CalculateCoverageSummary(
        IReadOnlyList<StaffingRequirement> requirements,
        IReadOnlyList<ShiftAssignment> assignments)
    {
        if (requirements.Count == 0)
        {
            return new ShiftCoverageSummaryViewModel
            {
                CoverageRate = null
            };
        }

        int totalRequiredSales = 0;
        int totalCoveredSales = 0;
        int totalRequiredCashier = 0;
        int totalCoveredCashier = 0;

        var assignmentGroups = assignments
            .GroupBy(a => (a.WorkDate, a.ShiftTypeId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var req in requirements)
        {
            totalRequiredSales += req.RequiredSalesStaff;
            totalRequiredCashier += req.RequiredCashiers;

            if (assignmentGroups.TryGetValue((req.WorkDate, req.ShiftTypeId), out var shiftAssigns))
            {
                int assignedSales = shiftAssigns.Count(a => a.Employee?.Position == EmployeePosition.Sales);
                int assignedCashier = shiftAssigns.Count(a => a.Employee?.Position == EmployeePosition.Cashier);

                // NC-08: Giới hạn slot đã phủ theo từng ngày-ca-vị trí để ca dư không bù ca thiếu.
                totalCoveredSales += Math.Min(assignedSales, req.RequiredSalesStaff);
                totalCoveredCashier += Math.Min(assignedCashier, req.RequiredCashiers);
            }
        }

        int totalRequired = totalRequiredSales + totalRequiredCashier;
        int totalCovered = totalCoveredSales + totalCoveredCashier;

        return new ShiftCoverageSummaryViewModel
        {
            TotalRequiredSlots = totalRequired,
            TotalCoveredSlots = totalCovered,
            RequiredSalesSlots = totalRequiredSales,
            CoveredSalesSlots = totalCoveredSales,
            RequiredCashierSlots = totalRequiredCashier,
            CoveredCashierSlots = totalCoveredCashier,
            CoverageRate = totalRequired > 0 ? totalCovered * 100m / totalRequired : null
        };
    }
}
