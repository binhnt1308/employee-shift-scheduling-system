using Microsoft.Extensions.Logging.Abstractions;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Errors;
using ShiftScheduling.Models.ViewModels;
using ShiftScheduling.Tests.Fakes;

namespace ShiftScheduling.Tests.ShiftTypes;

public sealed class ShiftTypeServiceTests
{
    [Fact(DisplayName = "TC03-01 - UC03 BR03 - Hệ thống có đúng ba ca cố định")]
    public async Task GetAllAsync_ReturnsThreeFixedShifts()
    {
        // Sap xep
        var repository = CreateRepositoryWithThreeFixedShifts();
        var service = CreateService(repository, new FakeUnitOfWork());

        // Thuc hien
        var result = await service.GetAllAsync();

        // Kiem tra
        Assert.Equal(3, result.Count);
        Assert.Contains(result, s => s.ShiftCode == "MORNING");
        Assert.Contains(result, s => s.ShiftCode == "AFTERNOON");
        Assert.Contains(result, s => s.ShiftCode == "EVENING");
    }

    [Fact(DisplayName = "TC03-02 - UC03 BR03 - Ca sáng phải là 08:00-12:00")]
    public async Task GetEditAsync_MorningShift_HasFixedTime()
    {
        // Sap xep
        var repository = CreateRepositoryWithThreeFixedShifts();
        var service = CreateService(repository, new FakeUnitOfWork());

        // Thuc hien
        var result = await service.GetEditAsync(1);

        // Kiem tra
        Assert.NotNull(result);
        Assert.Equal("MORNING", result!.ShiftCode);
        Assert.Equal(new TimeOnly(8, 0), result.StartTime);
        Assert.Equal(new TimeOnly(12, 0), result.EndTime);
    }

    [Fact(DisplayName = "TC03-03 - UC03 BR03 - Ca chiều phải là 12:00-16:00")]
    public async Task GetEditAsync_AfternoonShift_HasFixedTime()
    {
        // Sap xep
        var repository = CreateRepositoryWithThreeFixedShifts();
        var service = CreateService(repository, new FakeUnitOfWork());

        // Thuc hien
        var result = await service.GetEditAsync(2);

        // Kiem tra
        Assert.NotNull(result);
        Assert.Equal("AFTERNOON", result!.ShiftCode);
        Assert.Equal(new TimeOnly(12, 0), result.StartTime);
        Assert.Equal(new TimeOnly(16, 0), result.EndTime);
    }

    [Fact(DisplayName = "TC03-04 - UC03 BR03 - Ca tối phải là 16:00-20:00")]
    public async Task GetEditAsync_EveningShift_HasFixedTime()
    {
        // Sap xep
        var repository = CreateRepositoryWithThreeFixedShifts();
        var service = CreateService(repository, new FakeUnitOfWork());

        // Thuc hien
        var result = await service.GetEditAsync(3);

        // Kiem tra
        Assert.NotNull(result);
        Assert.Equal("EVENING", result!.ShiftCode);
        Assert.Equal(new TimeOnly(16, 0), result.StartTime);
        Assert.Equal(new TimeOnly(20, 0), result.EndTime);
    }

    [Fact(DisplayName = "TC03-05 - UC03 BR03 - Từ chối thay đổi khung giờ ca cố định")]
    public async Task UpdateAsync_WhenChangingFixedTime_ReturnsInvalidTimeError()
    {
        // Sap xep
        var repository = CreateRepositoryWithThreeFixedShifts();
        var service = CreateService(repository, new FakeUnitOfWork());
        var model = new ShiftTypeEditViewModel
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng đổi giờ",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(11, 0),
            DisplayOrder = 1,
            IsActive = true
        };

        // Thuc hien
        var result = await service.UpdateAsync(1, model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTimeInvalid, result.ErrorCode);
    }

    [Fact(DisplayName = "TC03-06 - UC03 BR03 - Từ chối ngưng hoạt động ca cố định")]
    public async Task UpdateAsync_WhenDeactivatingFixedShift_ReturnsInvalidStatusError()
    {
        // Sap xep
        var repository = CreateRepositoryWithThreeFixedShifts();
        var service = CreateService(repository, new FakeUnitOfWork());
        var model = new ShiftTypeEditViewModel
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DisplayOrder = 1,
            IsActive = false
        };

        // Thuc hien
        var result = await service.UpdateAsync(1, model, 1);

        // Kiem tra
        Assert.False(result.Succeeded);
        Assert.Equal(ErrorCodes.ShiftTimeInvalid, result.ErrorCode);
    }

    private static FakeShiftTypeRepository CreateRepositoryWithThreeFixedShifts()
    {
        var repository = new FakeShiftTypeRepository();
        repository.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 1,
            ShiftCode = "MORNING",
            ShiftName = "Ca sáng",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            DurationHours = 4,
            DisplayOrder = 1,
            IsActive = true
        });
        repository.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 2,
            ShiftCode = "AFTERNOON",
            ShiftName = "Ca chiều",
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(16, 0),
            DurationHours = 4,
            DisplayOrder = 2,
            IsActive = true
        });
        repository.ShiftTypes.Add(new ShiftType
        {
            ShiftTypeId = 3,
            ShiftCode = "EVENING",
            ShiftName = "Ca tối",
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(20, 0),
            DurationHours = 4,
            DisplayOrder = 3,
            IsActive = true
        });
        return repository;
    }

    private static ShiftTypeService CreateService(
        FakeShiftTypeRepository repository,
        FakeUnitOfWork unitOfWork)
    {
        return new ShiftTypeService(
            repository,
            new FakeAuditLogRepository(),
            unitOfWork,
            new FakeClock(),
            NullLogger<ShiftTypeService>.Instance);
    }
}
