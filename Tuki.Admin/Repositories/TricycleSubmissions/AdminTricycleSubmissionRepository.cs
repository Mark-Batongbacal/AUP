using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tuki.Admin.Models.TricycleSubmissions;
using Tuki.Admin.Repositories.Common;

namespace Tuki.Admin.Repositories.TricycleSubmissions;

public sealed class AdminTricycleSubmissionRepository(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor) : IAdminTricycleSubmissionRepository
{
    public async Task<AdminRepositoryResult<AdminTricycleSubmissionPage>> GetPageAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/admin/tricycle-point-submissions?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status))
        {
            query += $"&status={Uri.EscapeDataString(status.Trim())}";
        }

        using var request = CreateRequest(HttpMethod.Get, query);
        return await SendJsonAsync<AdminTricycleSubmissionPage>(request, cancellationToken);
    }

    public async Task<AdminRepositoryResult<AdminTricycleSubmission>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/admin/tricycle-point-submissions/{id}");
        return await SendJsonAsync<AdminTricycleSubmission>(request, cancellationToken);
    }

    public async Task<AdminRepositoryResult<AdminTricycleSubmission>> UpdateReviewAsync(
        long id,
        AdminTricycleReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(HttpMethod.Put, $"api/admin/tricycle-point-submissions/{id}/review");
        message.Content = JsonContent.Create(request);
        return await SendJsonAsync<AdminTricycleSubmission>(message, cancellationToken);
    }

    public async Task<AdminRepositoryResult<AdminTricyclePublication>> ApproveAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"api/admin/tricycle-point-submissions/{id}/approve");
        return await SendJsonAsync<AdminTricyclePublication>(request, cancellationToken);
    }

    public Task<AdminRepositoryResult<AdminTricycleSubmission>> RejectAsync(
        long id,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendDecisionAsync(id, "reject", reason, cancellationToken);

    public Task<AdminRepositoryResult<AdminTricycleSubmission>> NeedsChangesAsync(
        long id,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendDecisionAsync(id, "needs-changes", reason, cancellationToken);

    public async Task<AdminRepositoryResult<ProofImageContent>> GetProofAsync(
        string proofImageUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proofImageUrl) ||
            !proofImageUrl.StartsWith("/api/tricycle-point-submissions/proof/", StringComparison.Ordinal))
        {
            return AdminRepositoryResult<ProofImageContent>.Failure(400, "Invalid proof image reference.");
        }

        using var request = CreateRequest(HttpMethod.Get, proofImageUrl.TrimStart('/'));
        var client = httpClientFactory.CreateClient(BackendApiClientNames.TukiBackend);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return AdminRepositoryResult<ProofImageContent>.Failure(
                (int)response.StatusCode,
                "Unable to load the proof image.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return AdminRepositoryResult<ProofImageContent>.Success(
            new ProofImageContent(bytes, contentType),
            (int)response.StatusCode);
    }

    private async Task<AdminRepositoryResult<AdminTricycleSubmission>> SendDecisionAsync(
        long id,
        string action,
        string reason,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"api/admin/tricycle-point-submissions/{id}/{action}");
        request.Content = JsonContent.Create(new AdminDecisionRequest(reason));
        return await SendJsonAsync<AdminTricycleSubmission>(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Admin HTTP context is unavailable.");
        var apiKey = context.Session.GetString("TukiAdminApiKey");
        var headerName = context.Session.GetString("TukiAdminApiKeyHeader");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(headerName))
        {
            throw new InvalidOperationException("The Admin backend session has expired. Sign in again.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(headerName, apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<AdminRepositoryResult<T>> SendJsonAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(BackendApiClientNames.TukiBackend);
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return value is null
                ? AdminRepositoryResult<T>.Failure((int)response.StatusCode, "The backend returned an empty response.")
                : AdminRepositoryResult<T>.Success(value, (int)response.StatusCode);
        }

        var backendError = await TryReadBackendErrorAsync(response, cancellationToken);
        return AdminRepositoryResult<T>.Failure(
            (int)response.StatusCode,
            backendError ?? $"Backend request failed with status {(int)response.StatusCode}.");
    }

    private static async Task<string?> TryReadBackendErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<BackendErrorResponse>(
                cancellationToken: cancellationToken);
            return error?.Errors is { Count: > 0 }
                ? string.Join(" ", error.Errors)
                : null;
        }
        catch
        {
            return null;
        }
    }
}
