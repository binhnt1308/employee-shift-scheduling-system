using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Infrastructure.Persistence.Seed;

public static class ModelSeed
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>().HasData(CreateAccounts());
        modelBuilder.Entity<Employee>().HasData(CreateEmployees());
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
            },
            new UserAccount
            {
                UserAccountId = 3,
                Username = "nhanvien",
                PasswordHash = "100000.o2UkhUfzacTkDzOTs2NgPQ==.H/cGgTusNfv1PTAS9pTbBKEfNKMSw5IurN0GUEwEh90=",
                Role = UserRole.Employee,
                IsLocked = false,
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedByAccountId = 1
            }
        ];
    }

    private static Employee[] CreateEmployees()
    {
        return
        [
            new Employee
            {
                EmployeeId = 1,
                UserAccountId = 3,
                EmployeeCode = "EMP001",
                FullName = "Nguyễn Văn Nhân Viên",
                DateOfBirth = new DateOnly(2000, 1, 1),
                PhoneNumber = "0901234567",
                Email = "nhanvien@example.com",
                Position = EmployeePosition.Sales,
                EmploymentType = EmploymentType.FullTime,
                Level = EmployeeLevel.Level2,
                StartDate = new DateOnly(2026, 1, 1),
                IsActive = true
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
