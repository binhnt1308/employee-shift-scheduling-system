using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

// Tạo dữ liệu nền tối thiểu như tài khoản hệ thống và ba ca cố định trước khi chạy các kịch bản demo.
public static class ModelSeed
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>().HasData(CreateAccounts());
        modelBuilder.Entity<ShiftType>().HasData(CreateShiftTypes());
    }

    private static UserAccount[] CreateAccounts()
    {
        return
        [
            new UserAccount
            {
                UserAccountId = 1,
                Username = "admin",
                PasswordHash = "100000.ABEiM0RVZneImaq7zN3u/w==.voGHdh8BUEr7qGOgntpkfAlsfKyMx/CzNR/DLboeAU4=",
                Role = UserRole.Admin,
                IsLocked = false,
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new UserAccount
            {
                UserAccountId = 2,
                Username = "quanly",
                PasswordHash = "100000./+7dzLuqmYh3ZlVEMyIRAA==.UWV+wqzCmlDi0tlbWmy/DpHdbaaFtpczTXzp5MErAog=",
                Role = UserRole.Manager,
                IsLocked = false,
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedByAccountId = 1
            }
        ];
    }

    private static ShiftType[] CreateShiftTypes()
    {
        return
        [
            new ShiftType
            {
                ShiftTypeId = 1,
                ShiftCode = "MORNING",
                ShiftName = "Ca sáng",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                DurationHours = 4,
                DisplayOrder = 1,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false
            },
            new ShiftType
            {
                ShiftTypeId = 2,
                ShiftCode = "AFTERNOON",
                ShiftName = "Ca chiều",
                StartTime = new TimeOnly(12, 0),
                EndTime = new TimeOnly(16, 0),
                DurationHours = 4,
                DisplayOrder = 2,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false
            },
            new ShiftType
            {
                ShiftTypeId = 3,
                ShiftCode = "EVENING",
                ShiftName = "Ca tối",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(20, 0),
                DurationHours = 4,
                DisplayOrder = 3,
                IsActive = true,
                MaxShiftsPerEmployeePerDay = 2,
                AllowSplitShift = false
            }
        ];
    }
}
