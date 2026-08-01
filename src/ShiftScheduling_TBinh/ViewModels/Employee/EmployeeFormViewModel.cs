using System.ComponentModel.DataAnnotations;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.ViewModels.Employee;

public class EmployeeFormViewModel
{
    public int EmployeeId { get; set; }

    [Display(Name = "Mã nhân viên")]
    [Required(ErrorMessage = "Vui lòng nhập mã nhân viên.")]
    [StringLength(30, ErrorMessage = "Mã nhân viên tối đa 30 ký tự.")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Display(Name = "Họ và tên")]
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Ngày sinh")]
    [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Số điện thoại")]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Display(Name = "Vị trí công việc")]
    [Required]
    public EmployeePosition Position { get; set; } = EmployeePosition.Sales;

    [Display(Name = "Hình thức làm việc")]
    [Required]
    public EmploymentType EmploymentType { get; set; } = EmploymentType.PartTime;

    [Display(Name = "Level năng lực")]
    [Required]
    public EmployeeLevel Level { get; set; } = EmployeeLevel.Level1;

    [Display(Name = "Ngày vào làm")]
    [Required(ErrorMessage = "Vui lòng chọn ngày vào làm.")]
    [DataType(DataType.Date)]
    public DateOnly? StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}
