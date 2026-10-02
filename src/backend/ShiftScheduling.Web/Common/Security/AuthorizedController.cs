using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ShiftScheduling.Common.Security;

// Controller nền đọc thông tin tài khoản/role từ Claims để các Controller con dùng thống nhất.
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
