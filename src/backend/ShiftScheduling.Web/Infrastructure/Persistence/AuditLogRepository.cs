using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Infrastructure.Persistence;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AuditLogRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(AuditLog auditLog)
    {
        _dbContext.AuditLogs.Add(auditLog);
    }
}
