using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ShiftScheduling.Features.Accounts.Controllers;
using ShiftScheduling.Features.Employees.Controllers;
using ShiftScheduling.Features.SchedulePeriods.Controllers;
using ShiftScheduling.Features.ShiftTypes.Controllers;
using ShiftScheduling.Models.Constants;

namespace ShiftScheduling.Tests.Common;

public sealed class RoleAuthorizationTests
{
    [Fact(DisplayName = "TC01-A01 - BR01 - AccountController Admin only")]
    public void AccountController_HasAuthorizeAttributeWithAdminRole()
    {
        var type = typeof(AccountController);
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.Admin, attribute?.Roles);
    }

    [Fact(DisplayName = "TC02-A01 - BR01 - EmployeeController Admin + Manager")]
    public void EmployeeController_HasAuthorizeAttributeWithAdminOrManagerRoles()
    {
        var type = typeof(EmployeeController);
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.AdminOrManager, attribute?.Roles);
    }

    [Fact(DisplayName = "TC03-A04 - BR01 - ShiftTypeController Manager only")]
    public void ShiftTypeController_HasAuthorizeAttributeWithManagerRoleOnly()
    {
        var type = typeof(ShiftTypeController);
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.Manager, attribute?.Roles);

        var roles = attribute?.Roles?.Split(',') ?? Array.Empty<string>();
        Assert.DoesNotContain(RoleNames.Admin, roles);
        Assert.DoesNotContain(RoleNames.Employee, roles);
    }

    [Fact(DisplayName = "TC03-A05 - BR01 - SchedulePeriodController Manager only")]
    public void SchedulePeriodController_HasAuthorizeAttributeWithManagerRole()
    {
        var type = typeof(SchedulePeriodController);
        var attribute = type.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.Manager, attribute?.Roles);
    }
}
