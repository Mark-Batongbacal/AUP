using Tuki.Admin.Models.ServerPerformance;

namespace Tuki.Admin.Repositories.ServerPerformance;

public interface IServerPerformanceRepository
{
    Task<ServerPerformanceRepositoryResult> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}

public sealed record ServerPerformanceRepositoryResult(
    bool Succeeded,
    ServerPerformanceSnapshot? Snapshot = null,
    int? StatusCode = null,
    string? ErrorMessage = null)
{
    public static ServerPerformanceRepositoryResult Success(ServerPerformanceSnapshot snapshot, int statusCode) =>
        new(true, snapshot, statusCode);

    public static ServerPerformanceRepositoryResult Failure(int? statusCode, string message) =>
        new(false, null, statusCode, message);
}
