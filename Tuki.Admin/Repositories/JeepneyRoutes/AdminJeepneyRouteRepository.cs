using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tuki.Admin.Models.JeepneyRoutes;
using Tuki.Admin.Repositories.Common;

namespace Tuki.Admin.Repositories.JeepneyRoutes;

public sealed class AdminJeepneyRouteRepository(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor) : IAdminJeepneyRouteRepository
{
    public Task<AdminJeepneyRepositoryResult<IReadOnlyList<AdminJeepneyRoute>>> GetAllAsync(
        bool includeActive = true,
        bool includeDrafts = true,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<IReadOnlyList<AdminJeepneyRoute>>(
            CreateRequest(
                HttpMethod.Get,
                $"api/admin/jeepney-routes?includeActive={includeActive.ToString().ToLowerInvariant()}&includeDrafts={includeDrafts.ToString().ToLowerInvariant()}"),
            cancellationToken);

    public Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> GetByIdAsync(
        long routeId,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<AdminJeepneyRoute>(
            CreateRequest(HttpMethod.Get, $"api/admin/jeepney-routes/{routeId}"),
            cancellationToken);

    public Task<AdminJeepneyRepositoryResult<AdminJeepneyRouteGeometry>> GetGeometryAsync(
        long routeId,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<AdminJeepneyRouteGeometry>(
            CreateRequest(HttpMethod.Get, $"api/admin/jeepney-routes/{routeId}/geometry"),
            cancellationToken);

    public Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> CreateDraftAsync(
        AdminJeepneyRouteRequest request,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(HttpMethod.Post, "api/admin/jeepney-routes", request, cancellationToken);

    public Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> UpdateDraftAsync(
        long routeId,
        AdminJeepneyRouteRequest request,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(HttpMethod.Put, $"api/admin/jeepney-routes/{routeId}", request, cancellationToken);

    public Task<AdminJeepneyRepositoryResult<AdminJeepneyRouteGeometry>> ReplaceDraftGeometryAsync(
        long routeId,
        AdminJeepneyRouteGeometryRequest request,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync<AdminJeepneyRouteGeometryRequest, AdminJeepneyRouteGeometry>(
            HttpMethod.Put,
            $"api/admin/jeepney-routes/{routeId}/geometry",
            request,
            cancellationToken);

    private Task<AdminJeepneyRepositoryResult<AdminJeepneyRoute>> SendMutationAsync(
        HttpMethod method,
        string path,
        AdminJeepneyRouteRequest request,
        CancellationToken cancellationToken) =>
        SendMutationAsync<AdminJeepneyRouteRequest, AdminJeepneyRoute>(method, path, request, cancellationToken);

    private async Task<AdminJeepneyRepositoryResult<TResponse>> SendMutationAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(method, path);
        message.Content = JsonContent.Create(request);
        return await SendJsonAsync<TResponse>(message, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Admin HTTP context is unavailable.");
        var apiKey = context.Session.GetString("TukiAdminApiKey");
        var headerName = context.Session.GetString("TukiAdminApiKeyHeader");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(headerName))
            throw new InvalidOperationException("The Admin backend session has expired. Sign in again.");

        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(headerName, apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<AdminJeepneyRepositoryResult<T>> SendJsonAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(BackendApiClientNames.TukiBackend);
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return value is null
                ? AdminJeepneyRepositoryResult<T>.Failure((int)response.StatusCode, "The backend returned an empty response.")
                : AdminJeepneyRepositoryResult<T>.Success(value, (int)response.StatusCode);
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<AdminJeepneyBackendError>(cancellationToken: cancellationToken);
            if (error?.Errors is { Count: > 0 })
                return AdminJeepneyRepositoryResult<T>.Failure((int)response.StatusCode, string.Join(" ", error.Errors));
        }
        catch
        {
        }

        return AdminJeepneyRepositoryResult<T>.Failure(
            (int)response.StatusCode,
            $"Backend request failed with status {(int)response.StatusCode}.");
    }
}
