using Tuki.Admin.Models.Auth;

namespace Tuki.Admin.Services.BackendApiClient;

public interface IBackendApiClientService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
