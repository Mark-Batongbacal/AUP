using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tuki.Admin.Models.ServerPerformance;
using Tuki.Admin.Repositories.Common;

namespace Tuki.Admin.Repositories.ServerPerformance;

public sealed class ServerPerformanceRepository(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor) : IServerPerformanceRepository
{
    public async Task<ServerPerformanceRepositoryResult> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Admin HTTP context is unavailable.");
        var apiKey = context.Session.GetString("TukiAdminApiKey");
        var headerName = context.Session.GetString("TukiAdminApiKeyHeader");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(headerName))
        {
            return ServerPerformanceRepositoryResult.Failure(
                (int)HttpStatusCode.Unauthorized,
                "The Admin backend session has expired. Sign in again.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/system/overview");
        request.Headers.TryAddWithoutValidation(headerName, apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = httpClientFactory.CreateClient(BackendApiClientNames.TukiBackend);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ServerPerformanceRepositoryResult.Failure(
                    (int)response.StatusCode,
                    $"The monitoring endpoint returned HTTP {(int)response.StatusCode}.");
            }

            try
            {
                var snapshot = await response.Content.ReadFromJsonAsync<ServerPerformanceSnapshot>(
                    cancellationToken: cancellationToken);
                return snapshot is null
                    ? ServerPerformanceRepositoryResult.Failure(
                        (int)HttpStatusCode.BadGateway,
                        "The backend returned an empty monitoring response.")
                    : ServerPerformanceRepositoryResult.Success(snapshot, (int)response.StatusCode);
            }
            catch (JsonException)
            {
                return ServerPerformanceRepositoryResult.Failure(
                    (int)HttpStatusCode.BadGateway,
                    "The backend returned an invalid monitoring response.");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServerPerformanceRepositoryResult.Failure(
                (int)HttpStatusCode.GatewayTimeout,
                "The backend monitoring request timed out.");
        }
        catch (HttpRequestException)
        {
            return ServerPerformanceRepositoryResult.Failure(
                (int)HttpStatusCode.BadGateway,
                "The Admin portal could not reach the TUKI backend.");
        }
        catch (IOException)
        {
            return ServerPerformanceRepositoryResult.Failure(
                (int)HttpStatusCode.BadGateway,
                "The monitoring connection ended unexpectedly.");
        }
    }
}
