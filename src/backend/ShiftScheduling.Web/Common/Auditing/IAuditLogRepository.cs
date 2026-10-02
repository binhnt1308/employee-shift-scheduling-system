using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Common.Auditing;

// Khai báo thao tác lưu AuditLog; nghiệp vụ quyết định nội dung log nằm ở Service.
public interface IAuditLogRepository
{
    void Add(AuditLog auditLog);
}
