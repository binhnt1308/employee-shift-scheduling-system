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

    [Fact(DisplayName = "TC05-06 - Lịch trống có định mức 1 Tư vấn, 1 Thu ngân, Level 2+ -> IsValid = false, thiếu cả 3 yêu cầu")]
    public void ValidateSchedule_WhenEmptyScheduleWithStaffingRequirements_ReturnsInvalidWithAllRequiredErrors()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement
            {
                WorkDate = period.WeekStartDate,
                ShiftTypeId = 1,
                ShiftType = shiftTypes[0],
                RequiredSalesStaff = 1,
                RequiredCashiers = 1
            }
        };

        var result = _service.ValidateSchedule(
            period,
            Array.Empty<Employee>(),
            new List<Availability>(),
            new List<LeaveRequest>(),
            requirements,
            new List<ShiftAssignment>(),
            shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("Tư vấn"));
        Assert.Contains(result.HardErrors, e => e.Contains("Thu ngân"));
        Assert.Contains(result.HardErrors, e => e.Contains("Level 2"));
    }

    [Fact(DisplayName = "TC05-07 - Chỉ đủ Tư vấn nhưng thiếu Thu ngân -> Không hợp lệ")]
    public void ValidateSchedule_WhenOnlySalesFulfilledButMissingCashier_ReturnsInvalid()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var empSales = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "Tư Vấn 1",
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
            period, new[] { empSales }, avails, new List<LeaveRequest>(), requirements, assignments, shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("Thu ngân"));
    }

    [Fact(DisplayName = "TC05-08 - Đủ số người Tư vấn và Thu ngân nhưng không có Level 2+ -> Không hợp lệ")]
    public void ValidateSchedule_WhenStaffCountMetButNoLevel2Plus_ReturnsInvalid()
    {
        var period = CreateTestPeriod();
        var shiftTypes = CreateThreeFixedShifts();
        var empSales = new Employee
        {
            EmployeeId = 1,
            EmployeeCode = "NV01",
            FullName = "Tư Vấn 1",
            Position = EmployeePosition.Sales,
            Level = EmployeeLevel.Level1,
            IsActive = true
        };
        var empCashier = new Employee
        {
            EmployeeId = 2,
            EmployeeCode = "NV02",
            FullName = "Thu Ngân 1",
            Position = EmployeePosition.Cashier,
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
                RequiredSalesStaff = 1,
                RequiredCashiers = 1
            }
        };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new ShiftAssignment { EmployeeId = 2, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };
        var avails = new List<Availability>
        {
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new Availability { EmployeeId = 2, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var result = _service.ValidateSchedule(
            period, new[] { empSales, empCashier }, avails, new List<LeaveRequest>(), requirements, assignments, shiftTypes);

        Assert.False(result.IsValid);
        Assert.Contains(result.HardErrors, e => e.Contains("Level 2"));
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

    [Fact(DisplayName = "TC-VAL-OLD-01: Kỳ dùng ShiftType cũ IsActive = false, thiếu định mức -> Không bỏ qua requirement, IsValid = false")]
    public void ValidateSchedule_WhenPeriodUsesInactiveOldShiftsAndUnderstaffed_ReturnsInvalidWithHardErrors()
    {
        var period = CreateTestPeriod();

        // Ba ShiftType phiên bản cũ, IsActive = false (IDs 1, 2, 3)
        var oldShifts = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng (cũ)", StartTime = new(8, 0), EndTime = new(12, 0), DurationHours = 4m, ConfigurationVersion = 1, IsActive = false, DisplayOrder = 1 },
            new ShiftType { ShiftTypeId = 2, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều (cũ)", StartTime = new(12, 0), EndTime = new(16, 0), DurationHours = 4m, ConfigurationVersion = 1, IsActive = false, DisplayOrder = 2 },
            new ShiftType { ShiftTypeId = 3, ShiftCode = "EVENING", ShiftName = "Ca tối (cũ)", StartTime = new(16, 0), EndTime = new(20, 0), DurationHours = 4m, ConfigurationVersion = 1, IsActive = false, DisplayOrder = 3 }
        };

        // Ba ShiftType phiên bản mới, IsActive = true (IDs 4, 5, 6)
        var newShifts = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ShiftName = "Ca sáng (mới)", StartTime = new(7, 30), EndTime = new(11, 30), DurationHours = 4m, ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1 },
            new ShiftType { ShiftTypeId = 5, ShiftCode = "AFTERNOON", ShiftName = "Ca chiều (mới)", StartTime = new(11, 30), EndTime = new(15, 30), DurationHours = 4m, ConfigurationVersion = 2, IsActive = true, DisplayOrder = 2 },
            new ShiftType { ShiftTypeId = 6, ShiftCode = "EVENING", ShiftName = "Ca tối (mới)", StartTime = new(15, 30), EndTime = new(19, 30), DurationHours = 4m, ConfigurationVersion = 2, IsActive = true, DisplayOrder = 3 }
        };

        var allShifts = oldShifts.Concat(newShifts).ToList();

        // StaffingRequirements của kỳ chỉ tham chiếu ShiftTypeId phiên bản cũ (1, 2, 3)
        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement { WorkDate = period.WeekStartDate, ShiftTypeId = 1, ShiftType = oldShifts[0], RequiredSalesStaff = 2, RequiredCashiers = 1 },
            new StaffingRequirement { WorkDate = period.WeekStartDate, ShiftTypeId = 2, ShiftType = oldShifts[1], RequiredSalesStaff = 2, RequiredCashiers = 1 },
            new StaffingRequirement { WorkDate = period.WeekStartDate, ShiftTypeId = 3, ShiftType = oldShifts[2], RequiredSalesStaff = 1, RequiredCashiers = 1 }
        };

        // Lịch chỉ có 1 phân công cho Ca 1 (Level 1 Sales)
        var emp1 = new Employee { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level1, IsActive = true };
        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };
        var avails = new List<Availability>
        {
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var result = _service.ValidateSchedule(
            period, new[] { emp1 }, avails, new List<LeaveRequest>(), requirements, assignments, allShifts);

        // Validation không bỏ qua requirement phiên bản cũ
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.HardErrors);
        Assert.Contains(result.HardErrors, e => e.Contains("Thiếu 1 nhân viên Tư vấn"));
        Assert.Contains(result.HardErrors, e => e.Contains("Chưa có nhân viên kinh nghiệm"));
        Assert.Contains(result.HardErrors, e => e.Contains("Thiếu 1 nhân viên Thu ngân"));
    }

    [Fact(DisplayName = "TC-VAL-OLD-02: Kỳ dùng ShiftType cũ IsActive = false, đủ định mức -> IsValid = true")]
    public void ValidateSchedule_WhenPeriodUsesInactiveOldShiftsAndFullyStaffed_ReturnsValid()
    {
        var period = CreateTestPeriod();

        var oldShifts = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 1, ShiftCode = "MORNING", ShiftName = "Ca sáng (cũ)", StartTime = new(8, 0), EndTime = new(12, 0), DurationHours = 4m, ConfigurationVersion = 1, IsActive = false, DisplayOrder = 1 }
        };
        var newShifts = new List<ShiftType>
        {
            new ShiftType { ShiftTypeId = 4, ShiftCode = "MORNING", ShiftName = "Ca sáng (mới)", StartTime = new(7, 30), EndTime = new(11, 30), DurationHours = 4m, ConfigurationVersion = 2, IsActive = true, DisplayOrder = 1 }
        };
        var allShifts = oldShifts.Concat(newShifts).ToList();

        var requirements = new List<StaffingRequirement>
        {
            new StaffingRequirement { WorkDate = period.WeekStartDate, ShiftTypeId = 1, ShiftType = oldShifts[0], RequiredSalesStaff = 1, RequiredCashiers = 1 }
        };

        // 2 nhân viên: 1 Sales Level 2, 1 Cashier Level 1
        var emp1 = new Employee { EmployeeId = 1, EmployeeCode = "NV01", FullName = "NV1", Position = EmployeePosition.Sales, Level = EmployeeLevel.Level2, IsActive = true };
        var emp2 = new Employee { EmployeeId = 2, EmployeeCode = "NV02", FullName = "NV2", Position = EmployeePosition.Cashier, Level = EmployeeLevel.Level1, IsActive = true };

        var assignments = new List<ShiftAssignment>
        {
            new ShiftAssignment { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new ShiftAssignment { EmployeeId = 2, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };
        var avails = new List<Availability>
        {
            new Availability { EmployeeId = 1, WorkDate = period.WeekStartDate, ShiftTypeId = 1 },
            new Availability { EmployeeId = 2, WorkDate = period.WeekStartDate, ShiftTypeId = 1 }
        };

        var result = _service.ValidateSchedule(
            period, new[] { emp1, emp2 }, avails, new List<LeaveRequest>(), requirements, assignments, allShifts);

        Assert.True(result.IsValid);
        Assert.Empty(result.HardErrors);
    }
}
