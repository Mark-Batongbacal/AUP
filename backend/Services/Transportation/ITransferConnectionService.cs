using backend.Models.Database;

namespace backend.Services.Transportation;

public interface ITransferConnectionService
{
    Task<List<TransferConnection>> GetAllActiveConnectionsAsync(
        CancellationToken cancellationToken = default);

    Task<TransferConnection?> GetConnectionByIdAsync(
        long transferConnectionId,
        CancellationToken cancellationToken = default);

    Task<List<TransferConnection>> GetActiveConnectionsForStopAsync(
        long stopId,
        CancellationToken cancellationToken = default);

    Task<TransferConnectionMutationResult> AddVerifiedTransferConnectionAsync(
        long fromStopId,
        long toStopId,
        int? maximumWalkingDistanceMeters = null,
        int? estimatedWalkingTimeSeconds = null,
        string? instructions = null,
        bool isBidirectional = true,
        bool isActive = true,
        CancellationToken cancellationToken = default);

    Task<TransferConnectionMutationResult> UpdateVerifiedTransferConnectionAsync(
        long transferConnectionId,
        long fromStopId,
        long toStopId,
        int? maximumWalkingDistanceMeters = null,
        int? estimatedWalkingTimeSeconds = null,
        string? instructions = null,
        bool isBidirectional = true,
        bool isActive = true,
        CancellationToken cancellationToken = default);
}

public enum TransferConnectionMutationStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Duplicate,
}

public sealed record TransferConnectionMutationResult(
    TransferConnectionMutationStatus Status,
    IReadOnlyList<string> Errors,
    TransferConnection? TransferConnection)
{
    public static TransferConnectionMutationResult Success(TransferConnection transferConnection) =>
        new(TransferConnectionMutationStatus.Success, [], transferConnection);

    public static TransferConnectionMutationResult ValidationFailed(IReadOnlyList<string> errors) =>
        new(TransferConnectionMutationStatus.ValidationFailed, errors, null);

    public static TransferConnectionMutationResult NotFound(long transferConnectionId) =>
        new(
            TransferConnectionMutationStatus.NotFound,
            [$"Transfer connection {transferConnectionId} was not found."],
            null);

    public static TransferConnectionMutationResult Duplicate(long fromStopId, long toStopId) =>
        new(
            TransferConnectionMutationStatus.Duplicate,
            [$"An active transfer connection already exists between stops {fromStopId} and {toStopId}."],
            null);
}
