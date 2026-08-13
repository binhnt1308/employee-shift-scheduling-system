using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ShiftScheduling.Common.Security;

public abstract class AuthorizedController : Controller
{
    protected int CurrentAccountId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(value, out var accountId))
            {
                return accountId;
            }

            throw new InvalidOperationException("Không xác định được tài khoản đang đăng nhập.");
        }
    }
}
