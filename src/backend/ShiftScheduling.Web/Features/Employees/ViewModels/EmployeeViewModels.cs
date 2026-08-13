using System.ComponentModel.DataAnnotations;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Models.ViewModels;

public sealed class EmployeeFormViewModel
{
    public int EmployeeId { get; set; }

    [Display(Name = "Mã nhân viên")]
    [Required(ErrorMessage = "Vui lòng nhập mã nhân viên.")]
    [StringLength(30, MinimumLength = 2, ErrorMessage = "Mã nhân viên phải từ 2 đến 30 ký tự.")]
    [RegularExpression("^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Mã nhân viên chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Display(Name = "Họ và tên")]
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Ngày sinh")]
    [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Số điện thoại")]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(150, ErrorMessage = "Email tối đa 150 ký tự.")]
    public string? Email { get; set; }

    [Display(Name = "Vị trí công việc")]
    [EnumDataType(typeof(EmployeePosition), ErrorMessage = "Vị trí công việc không hợp lệ.")]
    public EmployeePosition Position { get; set; } = EmployeePosition.Sales;

    [Display(Name = "Hình thức làm việc")]
    [EnumDataType(typeof(EmploymentType), ErrorMessage = "Hình thức làm việc không hợp lệ.")]
    public EmploymentType EmploymentType { get; set; } = EmploymentType.PartTime;

    [Display(Name = "Level năng lực")]
    [EnumDataType(typeof(EmployeeLevel), ErrorMessage = "Level không hợp lệ.")]
    public EmployeeLevel Level { get; set; } = EmployeeLevel.Level1;

    [Display(Name = "Ngày vào làm")]
    [Required(ErrorMessage = "Vui lòng chọn ngày vào làm.")]
    [DataType(DataType.Date)]
    public DateOnly? StartDate { get; set; }
}

public sealed class EmployeeListItemViewModel
{
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public EmployeePosition Position { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public EmployeeLevel Level { get; init; }
    public bool IsActive { get; init; }

    public int ExpectedWeeklyHours => EmploymentType == EmploymentType.FullTime ? 48 : 20;
}

public sealed class EmployeeIndexViewModel
{
    public string? Keyword { get; init; }
    public PagedResult<EmployeeListItemViewModel> Employees { get; init; }
        = new(Array.Empty<EmployeeListItemViewModel>(), 1, 10, 0);
}
