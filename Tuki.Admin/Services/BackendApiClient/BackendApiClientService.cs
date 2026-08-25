using System.Net;
using System.Net.Http.Json;
using Tuki.Admin.Models.Auth;

namespace Tuki.Admin.Services.BackendApiClient;

public sealed class BackendApiClientService(HttpClient httpClient) : IBackendApiClientService
{
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
    }
}
