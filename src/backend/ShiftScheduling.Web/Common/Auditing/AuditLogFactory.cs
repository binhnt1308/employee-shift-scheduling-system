using System.Text.Json;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Common.Auditing;

public static class AuditLogFactory
{
    public static AuditLog Create(
        int actorAccountId,
        string action,
        string entityName,
        string entityId,
        object? previousValues,
        object? newValues,
        DateTime createdAtUtc)
    {
        return new AuditLog
        {
            ActorAccountId = actorAccountId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            PreviousValues = Serialize(previousValues),
            NewValues = Serialize(newValues),
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }

    private static string? Serialize(object? values)
    {
        return values is null
            ? null
            : JsonSerializer.Serialize(values);
    }
}
