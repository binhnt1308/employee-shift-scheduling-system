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
    private readonly IScheduleValidationService _validationService;
    private readonly ILogger<OrToolsSchedulingService> _logger;

    public OrToolsSchedulingService(
        IScheduleValidationService validationService,
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
        var fixedShifts = shiftTypes
            .Where(s => s.IsActive && IsFixedShiftCode(s.ShiftCode))
            .OrderBy(s => s.DisplayOrder)
            .ToList();

        if (fixedShifts.Count != 3 || !activeEmployees.Any())
        {
            return new OrToolsProposalResultViewModel
            {
                Succeeded = false,
                Status = "INFEASIBLE",
                Message = "Cần đủ 3 ca cố định và ít nhất 1 nhân viên đang hoạt động."
            };
        }

        var shiftCodeIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fixedShifts.Count; i++)
        {
            shiftCodeIndexMap[fixedShifts[i].ShiftCode] = i;
        }

        int morningIdx = shiftCodeIndexMap[ShiftTypeConstants.MorningCode];
        int afternoonIdx = shiftCodeIndexMap[ShiftTypeConstants.AfternoonCode];
        int eveningIdx = shiftCodeIndexMap[ShiftTypeConstants.EveningCode];

        var availSet = availabilities.Select(a => (a.EmployeeId, a.WorkDate, a.ShiftTypeId)).ToHashSet();
        var leaveSet = approvedLeaves
            .Where(l => l.Status == LeaveRequestStatus.Approved)
            .ToList();

        var reqMap = staffingRequirements
            .ToDictionary(r => (r.WorkDate, r.ShiftTypeId));

        var model = new CpModel();
        int empCount = activeEmployees.Count;
        int numDays = 7;
        int numShifts = 3;

        // BIEN QUYET DINH: x[e, d, s] in {0, 1}
        var x = new BoolVar[empCount, numDays, numShifts];

        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            for (int d = 0; d < numDays; d++)
            {
                var workDate = period.WeekStartDate.AddDays(d);
                for (int s = 0; s < numShifts; s++)
                {
                    var shift = fixedShifts[s];
                    var varName = $"x_{emp.EmployeeId}_{d}_{shift.ShiftTypeId}";
                    x[e, d, s] = model.NewBoolVar(varName);

                    // BR02/BR05: Chi phan vao ca da dang ky ranh
                    var isAvail = availSet.Contains((emp.EmployeeId, workDate, shift.ShiftTypeId));
                    // BR06: Khong phan va ca co don xin nghi da duyiet
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

        // RANG BUOC BAT BUOC TRONG CA (BR07, BR12, BR13)
        for (int d = 0; d < numDays; d++)
        {
            var workDate = period.WeekStartDate.AddDays(d);
            for (int s = 0; s < numShifts; s++)
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
                    if (emp.Position == EmployeePosition.Sales)
                    {
                        salesVars.Add(x[e, d, s]);
                    }
                    else if (emp.Position == EmployeePosition.Cashier)
                    {
                        cashierVars.Add(x[e, d, s]);
                    }

                    if (emp.Level >= EmployeeLevel.Level2)
                    {
                        lvl2PlusVars.Add(x[e, d, s]);
                    }
                }

                // BR12: Dinh muc Sales va Cashier
                model.Add(LinearExpr.Sum(salesVars) == salesReq);
                model.Add(LinearExpr.Sum(cashierVars) == cashierReq);

                // BR13: It nhat 1 nhan vien Level 2+ khi ca co nguoi
                if (salesReq + cashierReq > 0)
                {
                    model.Add(LinearExpr.Sum(lvl2PlusVars) >= 1);
                }
            }
        }

        // RANG BUOC NGUOI LAM (BR09, BR10, BR11)
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];

            if (emp.EmploymentType == EmploymentType.PartTime)
            {
                // BR09: PT max 1 shift/day
                for (int d = 0; d < numDays; d++)
                {
                    model.Add(x[e, d, 0] + x[e, d, 1] + x[e, d, 2] <= 1);
                }
            }
            else if (emp.EmploymentType == EmploymentType.FullTime)
            {
                // BR10: FT 2 ca lien tiep (Sang-Chieu hoac Chieu-Toi)
                for (int d = 0; d < numDays; d++)
                {
                    model.Add(x[e, d, morningIdx] + x[e, d, eveningIdx] <= 1);
                    model.Add(x[e, d, morningIdx] <= x[e, d, afternoonIdx]);
                    model.Add(x[e, d, eveningIdx] <= x[e, d, afternoonIdx]);
                    model.Add(x[e, d, morningIdx] + x[e, d, eveningIdx] == x[e, d, afternoonIdx]);
                }

                // BR11: FT max 6 days/week (Afternoon = 1 iff day is active)
                var ftDayVars = new List<BoolVar>();
                for (int d = 0; d < numDays; d++)
                {
                    ftDayVars.Add(x[e, d, afternoonIdx]);
                }
                model.Add(LinearExpr.Sum(ftDayVars) <= 6);
            }
        }

        // HAM MUC TIEU UU TIEN (BR18, BR19)
        var objTerms = new List<LinearExpr>();
        for (int e = 0; e < empCount; e++)
        {
            var emp = activeEmployees[e];
            var allEmpShifts = new List<BoolVar>();
            for (int d = 0; d < numDays; d++)
            {
                for (int s = 0; s < numShifts; s++)
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

        // CHAY CP-SAT SOLVER
        var solver = new CpSolver();
        solver.StringParameters = $"max_time_in_seconds:{timeoutSeconds}.0";

        var status = solver.Solve(model);
        var solveTime = (DateTime.UtcNow - startTime).TotalSeconds;

        _logger.LogInformation(
            "OR-Tools CP-SAT solve finished. Status: {Status}; Time: {Time:F2}s",
            status, solveTime);

        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
        {
            var proposedAssignments = new List<ProposedAssignmentItemViewModel>();
            var tempAssignments = new List<ShiftAssignment>();

            for (int e = 0; e < empCount; e++)
            {
                var emp = activeEmployees[e];
                for (int d = 0; d < numDays; d++)
                {
                    var workDate = period.WeekStartDate.AddDays(d);
                    for (int s = 0; s < numShifts; s++)
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

            var valResult = _validationService.ValidateSchedule(
                period, activeEmployees, availabilities, approvedLeaves, staffingRequirements, tempAssignments, shiftTypes);

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

        return new OrToolsProposalResultViewModel
        {
            Succeeded = false,
            Status = status.ToString(),
            Message = "Không tìm thấy phương án lập lịch thỏa mãn toàn bộ ràng buộc bắt buộc.",
            SolveTimeSeconds = solveTime
        };
    }

    private static bool IsFixedShiftCode(string? shiftCode)
    {
        if (string.IsNullOrWhiteSpace(shiftCode)) return false;
        return shiftCode.Equals(ShiftTypeConstants.MorningCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.AfternoonCode, StringComparison.OrdinalIgnoreCase)
            || shiftCode.Equals(ShiftTypeConstants.EveningCode, StringComparison.OrdinalIgnoreCase);
    }
}
