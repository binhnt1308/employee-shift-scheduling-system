using System.ComponentModel.DataAnnotations;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

public sealed class SchedulePeriodCreateViewModel
{
    [Display(Name = "Tên kỳ lịch")]
    public string? PeriodName { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu tuần.")]
    [Display(Name = "Ngày bắt đầu tuần")]
    public DateOnly WeekStartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Vui lòng nhập hạn đăng ký lịch rảnh.")]
    [Display(Name = "Hạn đăng ký lịch rảnh")]
    public DateTime AvailabilityDeadline { get; set; } = DateTime.Today.AddDays(3).AddHours(23).AddMinutes(59);
}
