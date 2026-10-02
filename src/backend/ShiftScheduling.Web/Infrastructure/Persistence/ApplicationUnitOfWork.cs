using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Models.Errors;

namespace ShiftScheduling.Infrastructure.Persistence;

public sealed class ApplicationUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public ApplicationUnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new DataConflictException(
                "Du lieu da thay doi boi mot thao tac khac.",
                exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // DB-13: Ràng buộc UNIQUE là lớp bảo vệ cuối cùng khi hai request đến đồng thời.
            throw new DataConflictException(
                "Du lieu bi trung voi ban ghi khac.",
                exception);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
