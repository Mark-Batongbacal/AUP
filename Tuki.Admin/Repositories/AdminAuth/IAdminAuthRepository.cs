using Tuki.Admin.Models.Auth;

namespace Tuki.Admin.Repositories.AdminAuth;

public interface IAdminAuthRepository
{
    Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
