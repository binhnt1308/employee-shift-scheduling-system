using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Tests.Infrastructure;

/// <summary>
/// Kiểm tra metadata EF Core đã cấu hình collation tiếng Việt cho FullName và ShiftName (LC-06).
/// </summary>
public sealed class VietnameseCollationMetadataTests
{
    [Fact(DisplayName = "TC-LC06-01 - Employee.FullName is configured with Vietnamese_100_CI_AI collation")]
    public void Employee_FullName_HasVietnameseCollation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Test;User Id=sa;Password=Pass;")
            .Options;

        using var dbContext = new ApplicationDbContext(options);
        var designModel = dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = designModel.FindEntityType(typeof(Employee));
        var property = entityType?.FindProperty("FullName");

        var collation = property?.GetCollation();

        Assert.NotNull(property);
        Assert.Equal("Vietnamese_100_CI_AI", collation);
    }

    [Fact(DisplayName = "TC-LC06-02 - ShiftType.ShiftName is configured with Vietnamese_100_CI_AI collation")]
    public void ShiftType_ShiftName_HasVietnameseCollation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=Test;User Id=sa;Password=Pass;")
            .Options;

        using var dbContext = new ApplicationDbContext(options);
        var designModel = dbContext.GetService<IDesignTimeModel>().Model;
        var entityType = designModel.FindEntityType(typeof(ShiftType));
        var property = entityType?.FindProperty("ShiftName");

        var collation = property?.GetCollation();

        Assert.NotNull(property);
        Assert.Equal("Vietnamese_100_CI_AI", collation);
    }
}
