using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.Authentication.Repositories;

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
