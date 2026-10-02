using System.ComponentModel.DataAnnotations;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

// Chứa các ViewModel dùng để trao đổi dữ liệu giữa Controller và View của chức năng kỳ lịch và định mức nhân sự.
public sealed class SchedulePeriodCreateViewModel
{
    [Display(Name = "Tên kỳ lịch")]
    public string? PeriodName { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu tuần.")]
    [Display(Name = "Ngày bắt đầu tuần")]
    public DateOnly WeekStartDate { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập hạn đăng ký lịch rảnh.")]
    [Display(Name = "Hạn đăng ký lịch rảnh")]
    public DateTime AvailabilityDeadline { get; set; }
}
