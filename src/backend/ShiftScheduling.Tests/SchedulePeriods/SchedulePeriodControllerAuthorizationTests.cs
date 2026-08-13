using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ShiftScheduling.Features.SchedulePeriods.Controllers;
using ShiftScheduling.Models.Constants;

namespace ShiftScheduling.Tests.SchedulePeriods;

public sealed class SchedulePeriodControllerAuthorizationTests
{
    [Fact(DisplayName = "TC03-A01 - UC03 BR01 - Manager được truy cập SchedulePeriodController")]
    public void SchedulePeriodController_HasAuthorizeAttributeWithManagerRole()
    {
        // Sap xep
        var type = typeof(SchedulePeriodController);

        // Thuc hien
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        // Kiem tra
        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.Manager, attribute?.Roles);
    }

    [Fact(DisplayName = "TC03-A02 - UC03 BR01 - Admin authenticated không được truy cập UC03")]
    public void SchedulePeriodController_RejectsAdminRole()
    {
        // Sap xep
        var type = typeof(SchedulePeriodController);

        // Thuc hien
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        // Kiem tra
        Assert.NotNull(attribute);
        var roles = attribute?.Roles?.Split(',') ?? Array.Empty<string>();
        Assert.DoesNotContain(RoleNames.Admin, roles);
    }

    [Fact(DisplayName = "TC03-A03 - UC03 BR01 - Employee authenticated không được truy cập UC03")]
    public void SchedulePeriodController_RejectsEmployeeRole()
    {
        // Sap xep
        var type = typeof(SchedulePeriodController);

        // Thuc hien
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        // Kiem tra
        Assert.NotNull(attribute);
        var roles = attribute?.Roles?.Split(',') ?? Array.Empty<string>();
        Assert.DoesNotContain(RoleNames.Employee, roles);
    }
}
