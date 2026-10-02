using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.Authentication.Repositories;

// Triển khai truy vấn EF Core cho đăng nhập và xác thực; chỉ chuẩn bị dữ liệu và tracking theo nhu cầu của Service.
public sealed class AuthenticationRepository : IAuthenticationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AuthenticationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserAccount?> GetByUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserAccounts
            .AsNoTracking()
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);
    }
}
