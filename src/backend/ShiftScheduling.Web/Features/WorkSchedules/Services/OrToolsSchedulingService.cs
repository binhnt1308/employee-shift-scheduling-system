using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Service sinh lich de xuat tu dong bang Google OR-Tools CP-SAT (UC06/BR15).
/// </summary>
public sealed class OrToolsSchedulingService : IOrToolsSchedulingService
{
    private readonly ISchedulingValidationService _validationService;
    private readonly ILogger<OrToolsSchedulingService> _logger;

    public OrToolsSchedulingService(
        ISchedulingValidationService validationService,
        ILogger<OrToolsSchedulingService> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public OrToolsProposalResultViewModel GenerateProposal(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        IReadOnlyList<ShiftType> shiftTypes,
        int timeoutSeconds = 10)
    {
        var startTime = DateTime.UtcNow;
        var fixedShifts = GetFixedShifts(shiftTypes);

        if (fixedShifts is null || !activeEmployees.Any())
        {
            return new OrToolsProposalResultViewModel
            {
                Succeeded = false,
                Status = "INFEASIBLE",
                Message = "Cần đủ 3 ca cố định và ít nhất 1 nhân viên đang hoạt động."
            };
        }

        var (model, x) = BuildCpModel(
            period, activeEmployees, fixedShifts, availabilities, approvedLeaves, staffingRequirements);

        var solver = new CpSolver { StringParameters = $"max_time_in_seconds:{timeoutSeconds}.0" };
        var status = solver.Solve(model);
        var solveTime = (DateTime.UtcNow - startTime).TotalSeconds;

        _logger.LogInformation(
            "OR-Tools CP-SAT solve finished. Status: {Status}; Time: {Time:F2}s",
            status, solveTime);

        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
        {
            return ProcessSolverSolution(
                solver, status, solveTime, x, period, activeEmployees, availabilities,
                approvedLeaves, staffingRequirements, fixedShifts, shiftTypes);
        }

        return new OrToolsProposalResultViewModel
        {
            Succeeded = false,
            Status = status.ToString(),
            Message = "Không tìm thấy phương án lập lịch thỏa mãn toàn bộ ràng buộc bắt buộc.",
            SolveTimeSeconds = solveTime
        };
    }

    private static List<ShiftType>? GetFixedShifts(IReadOnlyList<ShiftType> shiftTypes)
    {
        var fixedShifts = shiftTypes
            .Where(s => s.IsActive && IsFixedShiftCode(s.ShiftCode))
            .OrderBy(s => s.DisplayOrder)
            .ToList();

        return fixedShifts.Count == 3 ? fixedShifts : null;
    }

    private static (CpModel Model, BoolVar[,,] Variables) BuildCpModel(
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftType> fixedShifts,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements)
    {
        var shiftCodeIndexMap = BuildShiftCodeIndexMap(fixedShifts);
        int morningIdx = shiftCodeIndexMap[ShiftTypeConstants.MorningCode];
        int afternoonIdx = shiftCodeIndexMap[ShiftTypeConstants.AfternoonCode];
        int eveningIdx = shiftCodeIndexMap[ShiftTypeConstants.EveningCode];

        var availSet = availabilities.Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId)).ToHashSet();
        var leaveSet = approvedLeaves.Where(l => l.Status == LeaveRequestStatus.Approved).ToList();
        var reqMap = staffingRequirements.ToDictionary(r => (r.WorkDate, r.ShiftTypeId));

        var model = new CpModel();
        int empCount = activeEmployees.Count;
        var x = new BoolVar[empCount, 7, 3];

        AddAvailabilityAndLeaveConstraints(model, x, activeEmployees, period, fixedShifts, availSet, leaveSet);
        AddStaffingRequirementsConstraints(model, x, activeEmployees, period, fixedShifts, reqMap);
        AddEmploymentWorkloadConstraints(model, x, activeEmployees, morningIdx, afternoonIdx, eveningIdx);
        AddWorkloadObjective(model, x, activeEmployees);

        return (model, x);
    }

    private static Dictionary<string, int> BuildShiftCodeIndexMap(List<ShiftType> fixedShifts)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fixedShifts.Count; i++)
        {
            map[fixedShifts[i].ShiftCode] = i;
        }
        return map;
    }

    private static void AddAvailabilityAndLeaveConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        HashSet<(int EmployeeId, DateOnly WorkDate, int ShiftTypeId)> availSet,
        List<LeaveRequest> leaveSet)
    {
        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            for (int d = 0; d < 7; d++)
            {
                var workDate = period.WeekStartDate.AddDays(d);
                for (int s = 0; s < 3; s++)
                {
                    var shift = fixedShifts[s];
                    x[e, d, s] = model.NewBoolVar($"x_{emp.EmployeeId}_{d}_{shift.ShiftTypeId}");

                    var isAvail = availSet.Contains((emp.EmployeeId, workDate, shift.ShiftTypeId));
                    var isOnLeave = leaveSet.Any(l => l.EmployeeId == emp.EmployeeId
                        && l.LeaveDate == workDate
                        && (!l.ShiftTypeId.HasValue || l.ShiftTypeId.Value == shift.ShiftTypeId));

                    if (!isAvail || isOnLeave || !emp.IsActive)
                    {
                        model.Add(x[e, d, s] == 0);
                    }
                }
            }
        }
    }

    private static void AddStaffingRequirementsConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        SchedulePeriod period,
        List<ShiftType> fixedShifts,
        Dictionary<(DateOnly WorkDate, int ShiftTypeId), StaffingRequirement> reqMap)
    {
        int empCount = activeEmployees.Count;
        for (int d = 0; d < 7; d++)
        {
            var workDate = period.WeekStartDate.AddDays(d);
            for (int s = 0; s < 3; s++)
            {
                var shift = fixedShifts[s];
                reqMap.TryGetValue((workDate, shift.ShiftTypeId), out var req);
                int salesReq = req?.RequiredSalesStaff ?? 0;
                int cashierReq = req?.RequiredCashiers ?? 0;

                var salesVars = new List<BoolVar>();
                var cashierVars = new List<BoolVar>();
                var lvl2PlusVars = new List<BoolVar>();

                for (int e = 0; e < empCount; e++)
                {
                    var emp = activeEmployees[e];
                    if (emp.Position == EmployeePosition.Sales) salesVars.Add(x[e, d, s]);
                    else if (emp.Position == EmployeePosition.Cashier) cashierVars.Add(x[e, d, s]);

                    if (emp.Level >= EmployeeLevel.Level2) lvl2PlusVars.Add(x[e, d, s]);
                }

                model.Add(LinearExpr.Sum(salesVars) == salesReq);
                model.Add(LinearExpr.Sum(cashierVars) == cashierReq);

                if (salesReq + cashierReq > 0)
                {
                    model.Add(LinearExpr.Sum(lvl2PlusVars) >= 1);
                }
            }
        }
    }

    private static void AddEmploymentWorkloadConstraints(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees,
        int morningIdx,
        int afternoonIdx,
        int eveningIdx)
    {
        int empCount = activeEmployees.Count;
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            if (emp.EmploymentType == EmploymentType.PartTime)
            {
                for (int d = 0; d < 7; d++)
                {
                    model.Add(x[e, d, 0] + x[e, d, 1] + x[e, d, 2] <= 1);
                }
            }
            else if (emp.EmploymentType == EmploymentType.FullTime)
            {
                for (int d = 0; d < 7; d++)
                {
                    model.Add(x[e, d, morningIdx] + x[e, d, eveningIdx] <= 1);
                    model.Add(x[e, d, morningIdx] <= x[e, d, afternoonIdx]);
                    model.Add(x[e, d, eveningIdx] <= x[e, d, afternoonIdx]);
                    model.Add(x[e, d, morningIdx] + x[e, d, eveningIdx] == x[e, d, afternoonIdx]);
                }

                var ftDayVars = new List<BoolVar>();
                for (int d = 0; d < 7; d++)
                {
                    ftDayVars.Add(x[e, d, afternoonIdx]);
                }
                model.Add(LinearExpr.Sum(ftDayVars) <= 6);
            }
        }
    }

    private static void AddWorkloadObjective(
        CpModel model,
        BoolVar[,,] x,
        IReadOnlyList<Employee> activeEmployees)
    {
        int empCount = activeEmployees.Count;
        var objTerms = new List<LinearExpr>();

        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            var allEmpShifts = new List<BoolVar>();
            for (int d = 0; d < 7; d++)
            {
                for (int s = 0; s < 3; s++)
                {
                    allEmpShifts.Add(x[e, d, s]);
                }
            }

            var totalEmpShifts = LinearExpr.Sum(allEmpShifts);
            int targetShifts = emp.EmploymentType == EmploymentType.PartTime ? 5 : 12;

            var diff = model.NewIntVar(-20, 20, $"diff_{e}");
            model.Add(diff == totalEmpShifts - targetShifts);

            var absDiff = model.NewIntVar(0, 20, $"abs_diff_{e}");
            model.AddAbsEquality(absDiff, diff);
            objTerms.Add(absDiff * 10);
        }

        model.Minimize(LinearExpr.Sum(objTerms));
    }

    private OrToolsProposalResultViewModel ProcessSolverSolution(
        CpSolver solver,
        CpSolverStatus status,
        double solveTime,
        BoolVar[,,] x,
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        IReadOnlyList<Availability> availabilities,
        IReadOnlyList<LeaveRequest> approvedLeaves,
        IReadOnlyList<StaffingRequirement> staffingRequirements,
        List<ShiftType> fixedShifts,
        IReadOnlyList<ShiftType> shiftTypes)
    {
        ExtractAssignments(
            solver, x, period, activeEmployees, fixedShifts,
            out var proposedAssignments, out var tempAssignments);

        var valResult = _validationService.ValidateSchedule(
            period, activeEmployees, availabilities, approvedLeaves,
            staffingRequirements, tempAssignments, shiftTypes);

        if (!valResult.IsValid)
        {
            return new OrToolsProposalResultViewModel
            {
                Succeeded = false,
                Status = "INFEASIBLE",
                Message = "Phương án đề xuất vi phạm ràng buộc bắt buộc.",
                SolveTimeSeconds = solveTime,
                ValidationResult = valResult
            };
        }

        return new OrToolsProposalResultViewModel
        {
            Succeeded = true,
            Status = status.ToString(),
            Message = $"Đã tạo lịch đề xuất tự động ({proposedAssignments.Count} lượt phân công).",
            SolveTimeSeconds = solveTime,
            ProposedAssignments = proposedAssignments,
            ValidationResult = valResult
        };
    }

    private static void ExtractAssignments(
        CpSolver solver,
        BoolVar[,,] x,
        SchedulePeriod period,
        IReadOnlyList<Employee> activeEmployees,
        List<ShiftType> fixedShifts,
        out List<ProposedAssignmentItemViewModel> proposedAssignments,
        out List<ShiftAssignment> tempAssignments)
    {
        proposedAssignments = new List<ProposedAssignmentItemViewModel>();
        tempAssignments = new List<ShiftAssignment>();
        int empCount = activeEmployees.Count;

        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            for (int d = 0; d < 7; d++)
            {
                var workDate = period.WeekStartDate.AddDays(d);
                for (int s = 0; s < 3; s++)
                {
                    var shift = fixedShifts[s];
                    if (solver.Value(x[e, d, s]) == 1)
                    {
                        proposedAssignments.Add(new ProposedAssignmentItemViewModel
                        {
                            WorkDate = workDate,
                            ShiftTypeId = shift.ShiftTypeId,
                            ShiftName = shift.ShiftName,
                            EmployeeId = emp.EmployeeId,
                            EmployeeCode = emp.EmployeeCode,
                            FullName = emp.FullName,
                            Position = emp.Position,
                            EmploymentType = emp.EmploymentType
                        });

                        tempAssignments.Add(new ShiftAssignment
                        {
                            EmployeeId = emp.EmployeeId,
                            WorkDate = workDate,
                            ShiftTypeId = shift.ShiftTypeId,
                            Source = AssignmentSource.ORTools
                        });
                    }
                }
            }
        }
    }

    private static bool IsFixedShiftCode(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode)) return false;
        return shiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase);
    }
}
