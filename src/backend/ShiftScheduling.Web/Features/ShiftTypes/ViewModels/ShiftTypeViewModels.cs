using System.ComponentModel.DataAnnotations;

namespace ShiftScheduling.Models.ViewModels;

public sealed class ShiftTypeEditViewModel
{
    public int ShiftTypeId { get; set; }

    [Display(Name = "Mã ca")]
    public string ShiftCode { get; set; } = string.Empty;

    [Display(Name = "Tên ca")]
    [Required(ErrorMessage = "Vui lòng nhập tên ca.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên ca phải từ 2 đến 100 ký tự.")]
    public string ShiftName { get; set; } = string.Empty;

    [Display(Name = "Giờ bắt đầu")]
    [DataType(DataType.Time)]
    public TimeOnly StartTime { get; set; }

    [Display(Name = "Giờ kết thúc")]
    [DataType(DataType.Time)]
    public TimeOnly EndTime { get; set; }

    [Display(Name = "Thứ tự hiển thị")]
    [Range(1, 20, ErrorMessage = "Thứ tự hiển thị phải từ 1 đến 20.")]
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class ShiftTypeListItemViewModel
{
    public int ShiftTypeId { get; init; }
    public string ShiftCode { get; init; } = string.Empty;
    public string ShiftName { get; init; } = string.Empty;
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public decimal DurationHours { get; init; }
    public int DisplayOrder { get; init; }
    public int ConfigurationVersion { get; init; } = 1;
    public bool IsActive { get; init; }
}

public sealed class ShiftConfigurationItemViewModel
{
    public int ShiftTypeId { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal DurationHours { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class ShiftConfigurationEditViewModel
{
    public int ExpectedConfigurationVersion { get; set; }
    public List<ShiftConfigurationItemViewModel> Shifts { get; set; } = new();
}
