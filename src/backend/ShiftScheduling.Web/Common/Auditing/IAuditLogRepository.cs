using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Common.Auditing;

public interface IAuditLogRepository
{
    void Add(AuditLog auditLog);
}
