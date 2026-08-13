using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ShiftScheduling.Features.Accounts.Controllers;
using ShiftScheduling.Features.Employees.Controllers;
using ShiftScheduling.Features.LeaveRequests.Controllers;
using ShiftScheduling.Features.SchedulePeriods.Controllers;
using ShiftScheduling.Features.ShiftTransfers.Controllers;
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

    [Fact(DisplayName = "TC09-A01 - BR01 - LeaveRequestController chỉ Manager + Employee")]
    public void LeaveRequestController_HasManagerOrEmployeeRoles()
    {
        var attribute = typeof(LeaveRequestController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.ManagerOrEmployee, attribute?.Roles);
    }

    [Fact(DisplayName = "TC10-A01 - BR01 - ShiftTransferController chỉ Manager + Employee")]
    public void ShiftTransferController_HasManagerOrEmployeeRoles()
    {
        var attribute = typeof(ShiftTransferController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.ManagerOrEmployee, attribute?.Roles);
    }

    [Fact(DisplayName = "TC09-A02 - BR01 - UC09 Create chỉ Employee")]
    public void LeaveRequestCreateActions_AreEmployeeOnly()
    {
        var actions = typeof(LeaveRequestController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(LeaveRequestController.Create))
            .ToArray();

        Assert.Equal(2, actions.Length);
        Assert.All(actions, action =>
        {
            var attribute = action.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(RoleNames.Employee, attribute?.Roles);
        });
    }

    [Fact(DisplayName = "TC09-A03 - BR01 - UC09 Review chỉ Manager")]
    public void LeaveRequestReviewActions_AreManagerOnly()
    {
        var actions = typeof(LeaveRequestController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(LeaveRequestController.Review))
            .ToArray();

        Assert.Equal(2, actions.Length);
        Assert.All(actions, action =>
        {
            var attribute = action.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(RoleNames.Manager, attribute?.Roles);
        });
    }

    [Fact(DisplayName = "TC10-A02 - BR01 - UC10 Create và Respond chỉ Employee")]
    public void ShiftTransferEmployeeActions_AreEmployeeOnly()
    {
        var actions = typeof(ShiftTransferController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(ShiftTransferController.Create)
                || method.Name == nameof(ShiftTransferController.Respond))
            .ToArray();

        Assert.Equal(3, actions.Length);
        Assert.All(actions, action =>
        {
            var attribute = action.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(RoleNames.Employee, attribute?.Roles);
        });
    }

    [Fact(DisplayName = "TC10-A03 - BR01 - UC10 Review chỉ Manager")]
    public void ShiftTransferReviewActions_AreManagerOnly()
    {
        var actions = typeof(ShiftTransferController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(ShiftTransferController.Review))
            .ToArray();

        Assert.Equal(2, actions.Length);
        Assert.All(actions, action =>
        {
            var attribute = action.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(RoleNames.Manager, attribute?.Roles);
        });
    }
}
