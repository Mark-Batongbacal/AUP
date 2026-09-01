using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tuki.Admin.Models.TricyclePoints;
using Tuki.Admin.Repositories.Common;

namespace Tuki.Admin.Repositories.TricyclePoints;

public sealed class AdminTricyclePointRepository(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor) : IAdminTricyclePointRepository
{
    public Task<AdminPointRepositoryResult<IReadOnlyList<AdminTricyclePoint>>> GetAllAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<IReadOnlyList<AdminTricyclePoint>>(
            CreateRequest(HttpMethod.Get, $"api/admin/tricycle-points?includeArchived={includeArchived.ToString().ToLowerInvariant()}"),
            cancellationToken);

    public Task<AdminPointRepositoryResult<AdminTricyclePoint>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<AdminTricyclePoint>(
            CreateRequest(HttpMethod.Get, $"api/admin/tricycle-points/{id}"),
            cancellationToken);

    public Task<AdminPointRepositoryResult<IReadOnlyList<TricyclePointDuplicateWarning>>> GetDuplicatesAsync(
        double latitude,
        double longitude,
        long? excludeId = null,
        double thresholdMeters = 75,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/admin/tricycle-points/duplicates?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&thresholdMeters={thresholdMeters.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (excludeId.HasValue) path += $"&excludeTricyclePointId={excludeId.Value}";
        return SendJsonAsync<IReadOnlyList<TricyclePointDuplicateWarning>>(
            CreateRequest(HttpMethod.Get, path), cancellationToken);
    }

    public Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> CreateAsync(
        AdminTricyclePointRequest request,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(HttpMethod.Post, "api/admin/tricycle-points", request, cancellationToken);

    public Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> UpdateAsync(
        long id,
        AdminTricyclePointRequest request,
        CancellationToken cancellationToken = default) =>
        SendMutationAsync(HttpMethod.Put, $"api/admin/tricycle-points/{id}", request, cancellationToken);

    public Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> ArchiveAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<AdminTricyclePointMutationResponse>(
            CreateRequest(HttpMethod.Post, $"api/admin/tricycle-points/{id}/archive"), cancellationToken);

    public Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> RestoreAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<AdminTricyclePointMutationResponse>(
            CreateRequest(HttpMethod.Post, $"api/admin/tricycle-points/{id}/restore"), cancellationToken);

    private async Task<AdminPointRepositoryResult<AdminTricyclePointMutationResponse>> SendMutationAsync(
        HttpMethod method, string path, AdminTricyclePointRequest request, CancellationToken cancellationToken)
    {
        using var message = CreateRequest(method, path);
        message.Content = JsonContent.Create(request);
        return await SendJsonAsync<AdminTricyclePointMutationResponse>(message, cancellationToken);
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

    private async Task<AdminPointRepositoryResult<T>> SendJsonAsync<T>(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(BackendApiClientNames.TukiBackend);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return value is null
                ? AdminPointRepositoryResult<T>.Failure((int)response.StatusCode, "The backend returned an empty response.")
                : AdminPointRepositoryResult<T>.Success(value, (int)response.StatusCode);
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<AdminBackendError>(cancellationToken: cancellationToken);
            if (error?.Errors is { Count: > 0 })
                return AdminPointRepositoryResult<T>.Failure((int)response.StatusCode, string.Join(" ", error.Errors));
        }
        catch { }

        return AdminPointRepositoryResult<T>.Failure((int)response.StatusCode, $"Backend request failed with status {(int)response.StatusCode}.");
    }
}
