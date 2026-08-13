using System.ComponentModel.DataAnnotations;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.ViewModels;

public sealed class AccountEmployeeOptionViewModel
{
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}

public sealed class AccountListItemViewModel
{
    public int UserAccountId { get; init; }
    public string Username { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsLocked { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedByUsername { get; init; }
    public string? EmployeeCode { get; init; }
    public string? EmployeeName { get; init; }
}

public sealed class AccountIndexViewModel
{
    public string? Keyword { get; init; }
    public PagedResult<AccountListItemViewModel> Accounts { get; init; }
        = new(Array.Empty<AccountListItemViewModel>(), 1, 10, 0);
}

public sealed class AccountCreateViewModel
{
    [Display(Name = "Tên đăng nhập")]
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 100 ký tự.")]
    [RegularExpression("^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Tên đăng nhập chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Nhập lại mật khẩu")]
    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Vai trò hệ thống")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "Vai trò không hợp lệ.")]
    public UserRole Role { get; set; } = UserRole.Employee;

    [Display(Name = "Hồ sơ nhân viên liên kết")]
    public int? EmployeeId { get; set; }

    public IReadOnlyList<AccountEmployeeOptionViewModel> EmployeeOptions { get; set; }
        = Array.Empty<AccountEmployeeOptionViewModel>();
}

public sealed class AccountEditViewModel
{
    public int UserAccountId { get; set; }

    [Display(Name = "Tên đăng nhập")]
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 100 ký tự.")]
    [RegularExpression("^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Tên đăng nhập chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Vai trò hệ thống")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "Vai trò không hợp lệ.")]
    public UserRole Role { get; set; }

    [Display(Name = "Hồ sơ nhân viên liên kết")]
    public int? EmployeeId { get; set; }

    public bool IsLocked { get; set; }

    public IReadOnlyList<AccountEmployeeOptionViewModel> EmployeeOptions { get; set; }
        = Array.Empty<AccountEmployeeOptionViewModel>();
}

public sealed class AccountResetPasswordViewModel
{
    public int UserAccountId { get; set; }
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu mới")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Nhập lại mật khẩu mới")]
    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
