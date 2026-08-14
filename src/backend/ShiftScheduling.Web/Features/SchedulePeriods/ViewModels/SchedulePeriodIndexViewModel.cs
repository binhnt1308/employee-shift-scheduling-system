using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.SchedulePeriods.ViewModels;

public sealed class SchedulePeriodIndexViewModel
{
    public PagedResult<SchedulePeriodListItemViewModel> Periods { get; set; }
        = new PagedResult<SchedulePeriodListItemViewModel>(
            Array.Empty<SchedulePeriodListItemViewModel>(), 0, 1, 10);
}
