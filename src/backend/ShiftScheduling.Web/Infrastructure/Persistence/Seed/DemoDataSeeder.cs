using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public sealed class DemoDataSet
{
    public List<UserAccount> Accounts { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
    public List<SchedulePeriod> Periods { get; set; } = new();
    public List<StaffingRequirement> StaffingRequirements { get; set; } = new();
    public List<Availability> Availabilities { get; set; } = new();
    public List<WeeklyAvailabilityPattern> WeeklyPatterns { get; set; } = new();
    public List<WorkSchedule> WorkSchedules { get; set; } = new();
    public List<ShiftAssignment> ShiftAssignments { get; set; } = new();
}

public static partial class DemoDataSeeder
{
    public const string DefaultDemoPassword = "Password123!";
    public const string PrimaryManagerUsername = "quanly";
    public const string PrimaryEmployeeCode = "NV02";
    public const string PrimaryEmployeeUsername = "nv02";
    public const string PrimaryEmployeeFullName = "Chiêm Trung Hậu";
    public const string PrimaryEmployeeEmail = "2351050042hau@ou.edu.vn";

    public static readonly string[] DemoEmployeeCodes =
    [
        "NV01", "NV02", "NV03", "NV04", "NV05", "NV06", "NV07", "NV08"
    ];

    public const string PublishedDemoPeriodMarker = "(Demo đã công bố)";
    public const string ReadyToScheduleDemoPeriodMarker = "(Demo sẵn sàng xếp lịch)";
    public const string RegistrationDemoPeriodMarker = "(Demo đang đăng ký)";
    public const string LegacyRegistrationDemoPeriodMarker = "(Demo đăng ký)";

    public static readonly string[] DemoAccountUsernames =
    [
        "admin", "quanly", "nv01", "nv02", "nv03", "nv04", "nv05", "nv06", "nv07", "nv08"
    ];

    /// <summary>
    /// Tính toán ngày Thứ Hai của 3 kỳ demo đại diện cho 3 giai đoạn nghiệp vụ:
    /// 1. Kỳ quá khứ đã công bố (tuần trước)
    /// 2. Kỳ sẵn sàng xếp lịch (tuần tới)
    /// 3. Kỳ đang mở đăng ký (tuần sau nữa)
    /// </summary>
    public static (DateOnly PastWeekStart, DateOnly ReadyWeekStart, DateOnly FutureWeekStart) CalculatePeriodDates(DateOnly today)
    {
        int daysFromMonday = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
        var currentMonday = today.AddDays(-daysFromMonday);
        var pastWeekStart = currentMonday.AddDays(-7);
        var readyWeekStart = currentMonday.AddDays(7);
        var futureWeekStart = currentMonday.AddDays(14);

        return (pastWeekStart, readyWeekStart, futureWeekStart);
    }

    /// <summary>
    /// Khởi tạo toàn bộ dữ liệu mẫu vào database nếu đáp ứng điều kiện an toàn.
    /// </summary>
    public static async Task<bool> SeedAsync(
        ApplicationDbContext dbContext,
        IPasswordHashService passwordHashService,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        // Kiểm tra điều kiện an toàn để tránh ghi đè dữ liệu nghiệp vụ hoặc tạo trùng dữ liệu
        var totalEmployees = await dbContext.Employees.CountAsync(cancellationToken);

        var existingPeriods = await dbContext.SchedulePeriods
            .Select(p => new { p.SchedulePeriodId, p.PeriodName })
            .ToListAsync(cancellationToken);

        var allPeriodNames = existingPeriods.Select(p => p.PeriodName).ToList();
        var decision = EvaluateSeedPrecondition(totalEmployees, allPeriodNames);

        if (decision != SeedDecision.ProceedWithFreshSeed)
        {
            return false;
        }

        var today = clock.TodayInVietnam;
        var (pastWeekStart, readyWeekStart, futureWeekStart) = CalculatePeriodDates(today);

        // Tránh xung đột ngày bắt đầu kỳ nếu người dùng đã tạo thủ công kỳ trùng ngày
        if (await dbContext.SchedulePeriods.AnyAsync(
            p => p.WeekStartDate == pastWeekStart || p.WeekStartDate == readyWeekStart || p.WeekStartDate == futureWeekStart, cancellationToken))
        {
            return false;
        }

        var isRelational = dbContext.Database.IsRelational();
        await using var transaction = isRelational
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var dataSet = BuildDemoDataSet(today, passwordHashService, clock);

            // Tra cứu tài khoản hiện có để tránh trùng tên đăng nhập đã được tạo sẵn
            var existingAccounts = await dbContext.UserAccounts.ToListAsync(cancellationToken);
            var existingUsernames = existingAccounts
                .Select(u => u.Username)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Liên kết nhân viên với tài khoản nếu tài khoản đã tồn tại
            foreach (var emp in dataSet.Employees)
            {
                if (emp.UserAccount != null)
                {
                    var existingAcc = existingAccounts.FirstOrDefault(
                        u => string.Equals(u.Username, emp.UserAccount.Username, StringComparison.OrdinalIgnoreCase));
                    if (existingAcc != null)
                    {
                        emp.UserAccount = existingAcc;
                        emp.UserAccountId = existingAcc.UserAccountId;
                    }
                }
            }

            // Chỉ tạo mới những tài khoản chưa có trong database
            var accountsToInsert = dataSet.Accounts
                .Where(a => !existingUsernames.Contains(a.Username))
                .ToList();

            if (accountsToInsert.Count > 0)
            {
                await dbContext.UserAccounts.AddRangeAsync(accountsToInsert, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Cập nhật UserAccountId cho nhân viên sau khi tài khoản đã được lưu
            foreach (var emp in dataSet.Employees)
            {
                if (emp.UserAccount != null && emp.UserAccount.UserAccountId > 0)
                {
                    emp.UserAccountId = emp.UserAccount.UserAccountId;
                }
            }

            // Tra cứu ca làm việc thực tế để gán đúng ShiftTypeId theo cấu hình hiện hành
            var activeShiftTypes = await dbContext.ShiftTypes
                .Where(st => st.IsActive)
                .OrderBy(st => st.DisplayOrder)
                .ToListAsync(cancellationToken);

            if (activeShiftTypes.Count >= 3)
            {
                var morningShift = activeShiftTypes.FirstOrDefault(st => st.ShiftCode == "MORNING") ?? activeShiftTypes[0];
                var afternoonShift = activeShiftTypes.FirstOrDefault(st => st.ShiftCode == "AFTERNOON") ?? activeShiftTypes[1];
                var eveningShift = activeShiftTypes.FirstOrDefault(st => st.ShiftCode == "EVENING") ?? activeShiftTypes[2];

                var shiftIdMap = new Dictionary<int, int>
                {
                    { 1, morningShift.ShiftTypeId },
                    { 2, afternoonShift.ShiftTypeId },
                    { 3, eveningShift.ShiftTypeId }
                };

                foreach (var req in dataSet.StaffingRequirements)
                {
                    if (shiftIdMap.TryGetValue(req.ShiftTypeId, out var realId))
                    {
                        req.ShiftTypeId = realId;
                    }
                }

                foreach (var avail in dataSet.Availabilities)
                {
                    if (shiftIdMap.TryGetValue(avail.ShiftTypeId, out var realId))
                    {
                        avail.ShiftTypeId = realId;
                    }
                }

                foreach (var assign in dataSet.ShiftAssignments)
                {
                    if (shiftIdMap.TryGetValue(assign.ShiftTypeId, out var realId))
                    {
                        assign.ShiftTypeId = realId;
                    }
                }
            }

            // Đồng bộ CreatedByAccountId theo tài khoản quản lý
            var managerAccount = existingAccounts.FirstOrDefault(u => string.Equals(u.Username, PrimaryManagerUsername, StringComparison.OrdinalIgnoreCase))
                ?? accountsToInsert.FirstOrDefault(u => string.Equals(u.Username, PrimaryManagerUsername, StringComparison.OrdinalIgnoreCase));
            int managerId = managerAccount?.UserAccountId > 0 ? managerAccount.UserAccountId : 2;

            foreach (var period in dataSet.Periods)
            {
                period.CreatedByAccountId = managerId;
            }
            foreach (var ws in dataSet.WorkSchedules)
            {
                ws.CreatedByAccountId = managerId;
                ws.PublishedByAccountId = managerId;
            }

            // Lưu toàn bộ các thực thể mẫu vào database
            await dbContext.Employees.AddRangeAsync(dataSet.Employees, cancellationToken);
            await dbContext.WeeklyAvailabilityPatterns.AddRangeAsync(dataSet.WeeklyPatterns, cancellationToken);
            await dbContext.SchedulePeriods.AddRangeAsync(dataSet.Periods, cancellationToken);
            await dbContext.StaffingRequirements.AddRangeAsync(dataSet.StaffingRequirements, cancellationToken);
            await dbContext.Availabilities.AddRangeAsync(dataSet.Availabilities, cancellationToken);
            await dbContext.WorkSchedules.AddRangeAsync(dataSet.WorkSchedules, cancellationToken);
            await dbContext.ShiftAssignments.AddRangeAsync(dataSet.ShiftAssignments, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }
}
