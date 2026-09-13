using ShiftScheduling.Models.Enums;
using ShiftScheduling.Models.ViewModels;

namespace ShiftScheduling.Features.Notifications.ViewModels;

public sealed record NotificationItemViewModel
{
    public int NotificationId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public NotificationType Type { get; init; }
    public int? RelatedObjectId { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime CreatedAtVietnam { get; init; }
    public string CreatedAtFormatted => CreatedAtVietnam.ToString("dd/MM/yyyy HH:mm");
    public string? TargetController => ResolveController(Type, RelatedObjectId);
    public string? TargetAction => ResolveAction(Type, RelatedObjectId);
    public int? TargetId => RelatedObjectId;

    private static string? ResolveController(NotificationType type, int? relatedObjectId)
    {
        if (!relatedObjectId.HasValue) return "Notification";

        return type switch
        {
            NotificationType.SchedulePublished => "MySchedule",
            NotificationType.ScheduleChanged => "MySchedule",
            NotificationType.LeaveRequestResult => "LeaveRequest",
            NotificationType.LeaveRequestSubmitted => "LeaveRequest",
            NotificationType.ShiftTransferRequest => "ShiftTransfer",
            NotificationType.ShiftTransferResult => "ShiftTransfer",
            NotificationType.RegistrationOpened => "Availability",
            _ => "Notification"
        };
    }

    private static string? ResolveAction(NotificationType type, int? relatedObjectId)
    {
        if (!relatedObjectId.HasValue) return "Index";

        return type switch
        {
            NotificationType.SchedulePublished => "Index",
            NotificationType.ScheduleChanged => "Index",
            NotificationType.LeaveRequestResult => "Details",
            NotificationType.LeaveRequestSubmitted => "Details",
            NotificationType.ShiftTransferRequest => "Details",
            NotificationType.ShiftTransferResult => "Details",
            NotificationType.RegistrationOpened => "Register",
            _ => "Index"
        };
    }
}

public sealed record NotificationIndexViewModel
{
    public PagedResult<NotificationItemViewModel> Notifications { get; init; } = new([], 1, 20, 0);
    public int UnreadCount { get; init; }
}
