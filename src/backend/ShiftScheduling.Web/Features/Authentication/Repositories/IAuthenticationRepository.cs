using ShiftScheduling.Models.Entities;

namespace ShiftScheduling.Features.Authentication.Repositories;

public interface IAuthenticationRepository
{
    Task<UserAccount?> GetByUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default);
}
