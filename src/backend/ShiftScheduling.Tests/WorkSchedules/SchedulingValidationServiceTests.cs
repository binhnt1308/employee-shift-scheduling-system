using ShiftScheduling.Features.WorkSchedules.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Tests.WorkSchedules;

public sealed class SchedulingValidationServiceTests
{
    private readonly SchedulingValidationService _service = new();

    [Fact(DisplayName = "TC05-01 - UC05 BR12 BR13 - Ca thiếu định mức Sales" +
        " và thiếu Level 2+ -> Trả về HardErrors")]
    public void ValidateSchedule_WhenUnderstaffedAndNoLevel2Plus_ReturnsHardErrors()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var emp1 = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "NV1",
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level1,
            IsActive = true
        };

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 1,
                ShiftType = shiftTypes[0],
                RequiredSalesStaff = 2,
                RequiredCashiers = 1
            }
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var avails = new List<Availability>
        {
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var result = _service.ValidateSchedule(
            period, new[] { emp1 }, avails, new List<LeaveRequest>(), requirements, assignments, shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("Thiếu 1 nhân viên Tư vấn"));
        Assert.Contains(result.HardErrors, e => e.Contains("Chưa có nhân viên kinh nghiệm"));
    }

    [Fact(DisplayName = "TC05-02 - UC05 BR05 - Phân công nhân viên chưa đăng ký rảnh" +
        " -> Trả về HardError")]
    public void ValidateSchedule_WhenEmployeeNotAvailable_ReturnsHardError()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var emp1 = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "NV1",
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 1,
                ShiftType = shiftTypes[0],
                RequiredSalesStaff = 1,
                RequiredCashiers = 0
            }
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var result = _service.ValidateSchedule(
            period,
            new[] { emp1 },
            new List<Availability>(),
            new List<LeaveRequest>(),
            requirements,
            assignments,
            shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("chưa đăng ký rảnh"));
    }

    [Fact(DisplayName = "TC05-03 - UC05 BR06 - Phân công nhân viên có đơn nghỉ đã duyệt" +
        " -> Trả về HardError")]
    public void ValidateSchedule_WhenEmployeeHasApprovedLeave_ReturnsHardError()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var emp1 = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "NV1",
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 1,
                ShiftType = shiftTypes[0],
                RequiredSalesStaff = 1,
                RequiredCashiers = 0
            }
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var avails = new List<Availability>
        {
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var leaves = new List<LeaveRequest>
        {
            new LeaveRequest
            {
                EmployeeId = 1,
                LeaveDate = period.WeekStartDate,
                ShiftTypeId = 1,
                Status = LeaveRequestStatus.Approved
            }
        };

        var result = _service.ValidateSchedule(
            period, new[] { emp1 }, avails, leaves, requirements, assignments, shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("có đơn nghỉ đã duyệt"));
    }

    [Fact(DisplayName = "TC05-04 - UC05 BR09 - Nhân viên PT bị phân 2 ca trong ngày -> Trả về HardError")]
    public void ValidateSchedule_WhenPartTimeHasTwoShiftsInOneDay_ReturnsHardError()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var emp1 = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "NV1",
            EmploymentType = EmploymentType.PartTime,
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 1,
                ShiftType = shiftTypes[0],
                RequiredSalesStaff = 1,
                RequiredCashiers = 0
            },
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 2,
                ShiftType = shiftTypes[1],
                RequiredSalesStaff = 1,
                RequiredCashiers = 0
            }
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 2 }
        };

        var avails = new List<Availability>
        {
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 2 }
        };

        var result = _service.ValidateSchedule(
            period, new[] { emp1 }, avails, new List<LeaveRequest>(), requirements, assignments, shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("tối đa 1 ca/ngày") || e.Contains("2 ca trong ngày"));
    }

    [Fact(DisplayName = "TC05-05 - UC05 BR10 - Nhân viên FT bị phân tổ hợp Sáng + Tối" +
        " -> Trả về HardError")]
    public void ValidateSchedule_WhenFullTimeHasMorningAndEvening_ReturnsHardError()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var emp1 = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "NV1",
            EmploymentType = EmploymentType.FullTime,
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level2,
            IsActive = true
        };

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 1,
                ShiftType = shiftTypes[0],
                RequiredSalesStaff = 1,
                RequiredCashiers = 0
            },
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 3,
                ShiftType = shiftTypes[2],
                RequiredSalesStaff = 1,
                RequiredCashiers = 0
            }
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 3 }
        };

        var avails = new List<Availability>
        {
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 3 }
        };

        var result = _service.ValidateSchedule(
            period, new[] { emp1 }, avails, new List<LeaveRequest>(), requirements, assignments, shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("không đạt tổ hợp 2 ca liên tiếp"));
    }

    private static SchedulePeriod CreateTestPeriod()
    {
        return new SchedulePeriod
        {
            SchedulePeriodId = 1,
            WeekStartDate = new DateOnly(2026, 8, 10),
            WeekEndDate = new DateOnly(2026, 8, 16)
        };
    }

    private static List<ShiftType> CreateThreeFixedShifts()
    {
        return new List<ShiftType>
        {
            new ShiftType
            {
                ShiftTypeId = 1,
                ShiftCode = "MORNING",
                ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                DurationHours = 4m,
                DisplayOrder = 1,
                IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 2,
                ShiftCode = "AFTERNOON",
                ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(16, 0),
                DurationHours = 4m,
                DisplayOrder = 2,
                IsActive = true
            },
            new ShiftType
            {
                ShiftTypeId = 3,
                ShiftCode = "EVENING",
                ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(20, 0),
                DurationHours = 4m,
                DisplayOrder = 3,
                IsActive = true
            }
        };
    }
}
