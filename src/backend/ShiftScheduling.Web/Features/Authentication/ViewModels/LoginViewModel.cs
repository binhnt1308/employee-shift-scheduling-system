using System.ComponentModel.DataAnnotations;

namespace ShiftScheduling.Models.ViewModels;

public sealed class LoginViewModel
{
    [Display(Name = "Tên đăng nhập")]
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập tối đa 100 ký tự.")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Mật khẩu")]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = "Mật khẩu tối đa 100 ký tự.")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
