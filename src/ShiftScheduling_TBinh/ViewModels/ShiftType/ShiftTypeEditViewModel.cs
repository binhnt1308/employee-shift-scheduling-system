using System.ComponentModel.DataAnnotations;

namespace ShiftScheduling.ViewModels.ShiftType;

public class ShiftTypeEditViewModel
{
    public int ShiftTypeId { get; set; }

    [Display(Name = "Mã ca")]
    public string ShiftCode { get; set; } = string.Empty;

    [Display(Name = "Tên ca")]
    [Required(ErrorMessage = "Vui lòng nhập tên ca.")]
    [StringLength(100)]
    public string ShiftName { get; set; } = string.Empty;

    [Display(Name = "Giờ bắt đầu")]
    [Required]
    [DataType(DataType.Time)]
    public TimeOnly StartTime { get; set; }

    [Display(Name = "Giờ kết thúc")]
    [Required]
    [DataType(DataType.Time)]
    public TimeOnly EndTime { get; set; }

    [Display(Name = "Thứ tự hiển thị")]
    [Range(1, 20)]
    public int DisplayOrder { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; }
}
