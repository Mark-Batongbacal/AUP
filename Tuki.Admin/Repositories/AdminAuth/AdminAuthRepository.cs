using System.Net;
using System.Net.Http.Json;
using Tuki.Admin.Models.Auth;
using Tuki.Admin.Repositories.Common;

namespace Tuki.Admin.Repositories.AdminAuth;

public sealed class AdminAuthRepository(IHttpClientFactory httpClientFactory) : IAdminAuthRepository
{
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(BackendApiClientNames.TukiBackend);
        using var response = await client.PostAsJsonAsync(
            "api/auth/admin/login",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(
            cancellationToken: cancellationToken);
    }
}
