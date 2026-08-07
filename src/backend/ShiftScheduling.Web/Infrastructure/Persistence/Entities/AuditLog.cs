namespace ShiftScheduling.Models.Entities;

public sealed class AuditLog
{
    public long AuditLogId { get; set; }
    public int ActorAccountId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? PreviousValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public UserAccount ActorAccount { get; set; } = null!;
}
