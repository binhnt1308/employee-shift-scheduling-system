using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

// Chứa các ViewModel dùng để trao đổi dữ liệu giữa Controller và View của chức năng kỳ lịch và định mức nhân sự.
public sealed class SchedulePeriodIndexViewModel
{
    public PagedResult<SchedulePeriodListItemViewModel> Periods { get; set; }
        = new PagedResult<SchedulePeriodListItemViewModel>(
            Array.Empty<SchedulePeriodListItemViewModel>(), 0, 1, 10);
}
