using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Constants;
using ShiftScheduling.Features.WorkSchedules.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.WorkSchedules.Services;

/// <summary>
/// Service sinh lịch đề xuất tự động bằng Google OR-Tools CP-SAT (UC06/BR15).
/// </summary>
public sealed partial class OrToolsSchedulingService : IOrToolsSchedulingService
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

        if (!TryValidateShiftDurations(fixedShifts, out var durationErrorMessage))
        {
            return new OrToolsProposalResultViewModel
            {
                Succeeded = false,
                Status = "INVALID_CONFIGURATION",
                Message = durationErrorMessage ?? "Cấu hình thời lượng ca không hợp lệ."
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
            Status = status.ToString().ToUpperInvariant(),
            Message = "Không tìm thấy phương án lập lịch thỏa mãn toàn bộ ràng buộc bắt buộc.",
            SolveTimeSeconds = solveTime
        };
    }

    private static bool TryValidateShiftDurations(
        List<ShiftType> fixedShifts,
        out string? errorMessage)
    {
        foreach (var shift in fixedShifts)
        {
            if (shift.DurationHours <= 0m)
            {
                errorMessage = $"Cấu hình ca '{shift.ShiftName}' có thời lượng không hợp lệ ({shift.DurationHours} giờ). Thời lượng phải lớn hơn 0.";
                return false;
            }

            var totalMinutes = shift.DurationHours * 60m;
            if (decimal.Remainder(totalMinutes, 1m) != 0m)
            {
                errorMessage = $"Cấu hình ca '{shift.ShiftName}' ({shift.DurationHours} giờ) không tương ứng với số phút nguyên ({totalMinutes:0.###} phút).";
                return false;
            }
        }

        errorMessage = null;
        return true;
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

        // BR-05: Giới hạn trần tổng giờ làm trong tuần theo loại hợp đồng (tính bằng phút nguyên)
        AddWeeklyMaxHoursConstraints(model, x, activeEmployees, fixedShifts);

        // BR21: Bảo đảm mỗi nhân viên có ít nhất 24 giờ nghỉ liên tục trong kỳ lịch 7 ngày.
        AddWeeklyContinuousRestConstraints(model, x, activeEmployees, period, fixedShifts);

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
            Status = status.ToString().ToUpperInvariant(),
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
