using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

// Nhóm logic hỗ trợ cho nghiệp vụ báo cáo ca và giờ làm.
public sealed partial class WorkReportService
{
    private static ShiftCoverageSummaryViewModel CalculateCoverageSummary(
        IReadOnlyList<StaffingRequirement> requirements,
        IReadOnlyList<ShiftAssignment> assignments,
        ApprovedLeaveLookup leaveLookup,
        int schedulePeriodId)
    {
        if (requirements.Count == 0)
        {
            return new ShiftCoverageSummaryViewModel { CoverageRate = null };
        }

        int totalRequiredSales = 0, totalCoveredSales = 0;
        int totalRequiredCashier = 0, totalCoveredCashier = 0;

        var assignmentGroups = assignments
            .Where(a => !leaveLookup.HasApprovedLeave(a.EmployeeId, schedulePeriodId, a.WorkDate, a.ShiftTypeId))
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

                totalCoveredSales += Math.Min(assignedSales, req.RequiredSalesStaff);
                totalCoveredCashier += Math.Min(assignedCashier, req.RequiredCashiers);
            }
        }

        return BuildCoverageSummary(
            totalRequiredSales, totalCoveredSales, totalRequiredCashier, totalCoveredCashier);
    }

    private static ShiftCoverageSummaryViewModel BuildCoverageSummary(
        int requiredSales, int coveredSales, int requiredCashier, int coveredCashier)
    {
        int totalRequired = requiredSales + requiredCashier;
        int totalCovered = coveredSales + coveredCashier;

        return new ShiftCoverageSummaryViewModel
        {
            TotalRequiredSlots = totalRequired,
            TotalCoveredSlots = totalCovered,
            RequiredSalesSlots = requiredSales,
            CoveredSalesSlots = coveredSales,
            RequiredCashierSlots = requiredCashier,
            CoveredCashierSlots = coveredCashier,
            CoverageRate = totalRequired > 0 ? totalCovered * 100m / totalRequired : null
        };
    }
}
