using backend.Models.Database;
using backend.Repositories;

namespace backend.Services.Transportation;

public sealed class TransferConnectionService(
    ITransferConnectionRepository transferConnectionRepository,
    ITransportStopRepository transportStopRepository) : ITransferConnectionService
{
    private readonly ITransferConnectionRepository _transferConnectionRepository = transferConnectionRepository;
    private readonly ITransportStopRepository _transportStopRepository = transportStopRepository;

    public Task<List<TransferConnection>> GetAllActiveConnectionsAsync(
        CancellationToken cancellationToken = default) =>
        _transferConnectionRepository.GetAllActiveAsync(cancellationToken);

    public Task<TransferConnection?> GetConnectionByIdAsync(
        long transferConnectionId,
        CancellationToken cancellationToken = default)
    {
        if (transferConnectionId <= 0)
        {
            return Task.FromResult<TransferConnection?>(null);
        }

        return _transferConnectionRepository.GetByIdAsync(transferConnectionId, cancellationToken);
    }

    public Task<List<TransferConnection>> GetActiveConnectionsForStopAsync(
        long stopId,
        CancellationToken cancellationToken = default)
    {
        if (stopId <= 0)
        {
            return Task.FromResult(new List<TransferConnection>());
        }

        return _transferConnectionRepository.GetActiveForStopAsync(stopId, cancellationToken);
    }

    public async Task<TransferConnectionMutationResult> AddVerifiedTransferConnectionAsync(
        long fromStopId,
        long toStopId,
        int? maximumWalkingDistanceMeters = null,
        int? estimatedWalkingTimeSeconds = null,
        string? instructions = null,
        bool isBidirectional = true,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await ValidateAsync(
            fromStopId,
            toStopId,
            maximumWalkingDistanceMeters,
            estimatedWalkingTimeSeconds,
            cancellationToken);
        if (validationErrors.Count > 0)
        {
            return TransferConnectionMutationResult.ValidationFailed(validationErrors);
        }

        if (await HasDuplicateConnectionAsync(
                fromStopId,
                toStopId,
                isBidirectional,
                existingConnectionId: null,
                cancellationToken))
        {
            return TransferConnectionMutationResult.Duplicate(fromStopId, toStopId);
        }

        var transferConnection = new TransferConnection
        {
            FromStopId = fromStopId,
            ToStopId = toStopId,
            MaximumWalkingDistanceMeters = maximumWalkingDistanceMeters,
            EstimatedWalkingTimeSeconds = estimatedWalkingTimeSeconds,
            Instructions = NormalizeOptionalText(instructions),
            IsBidirectional = isBidirectional,
            IsActive = isActive,
        };

        var createdConnection = await _transferConnectionRepository.AddAsync(
            transferConnection,
            cancellationToken);

        return TransferConnectionMutationResult.Success(createdConnection);
    }

    public async Task<TransferConnectionMutationResult> UpdateVerifiedTransferConnectionAsync(
        long transferConnectionId,
        long fromStopId,
        long toStopId,
        int? maximumWalkingDistanceMeters = null,
        int? estimatedWalkingTimeSeconds = null,
        string? instructions = null,
        bool isBidirectional = true,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        if (transferConnectionId <= 0)
        {
            return TransferConnectionMutationResult.NotFound(transferConnectionId);
        }

        var existingConnection = await _transferConnectionRepository.GetByIdAsync(
            transferConnectionId,
            cancellationToken);
        if (existingConnection is null)
        {
            return TransferConnectionMutationResult.NotFound(transferConnectionId);
        }

        var validationErrors = await ValidateAsync(
            fromStopId,
            toStopId,
            maximumWalkingDistanceMeters,
            estimatedWalkingTimeSeconds,
            cancellationToken);
        if (validationErrors.Count > 0)
        {
            return TransferConnectionMutationResult.ValidationFailed(validationErrors);
        }

        if (await HasDuplicateConnectionAsync(
                fromStopId,
                toStopId,
                isBidirectional,
                existingConnection.TransferConnectionId,
                cancellationToken))
        {
            return TransferConnectionMutationResult.Duplicate(fromStopId, toStopId);
        }

        existingConnection.FromStopId = fromStopId;
        existingConnection.ToStopId = toStopId;
        existingConnection.MaximumWalkingDistanceMeters = maximumWalkingDistanceMeters;
        existingConnection.EstimatedWalkingTimeSeconds = estimatedWalkingTimeSeconds;
        existingConnection.Instructions = NormalizeOptionalText(instructions);
        existingConnection.IsBidirectional = isBidirectional;
        existingConnection.IsActive = isActive;

        var updatedConnection = await _transferConnectionRepository.UpdateAsync(
            existingConnection,
            cancellationToken);

        return TransferConnectionMutationResult.Success(updatedConnection);
    }

    private async Task<List<string>> ValidateAsync(
        long fromStopId,
        long toStopId,
        int? maximumWalkingDistanceMeters,
        int? estimatedWalkingTimeSeconds,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (fromStopId <= 0)
        {
            errors.Add("Origin transport stop id must be greater than zero.");
        }

        if (toStopId <= 0)
        {
            errors.Add("Destination transport stop id must be greater than zero.");
        }

        if (fromStopId > 0 && toStopId > 0 && fromStopId == toStopId)
        {
            errors.Add("Origin and destination transport stops must be different.");
        }

        if (maximumWalkingDistanceMeters < 0)
        {
            errors.Add("Maximum walking distance cannot be negative.");
        }

        if (estimatedWalkingTimeSeconds < 0)
        {
            errors.Add("Estimated walking time cannot be negative.");
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var fromStop = await _transportStopRepository.GetByIdAsync(fromStopId, cancellationToken);
        if (fromStop is null)
        {
            errors.Add($"Origin transport stop {fromStopId} was not found.");
        }

        var toStop = await _transportStopRepository.GetByIdAsync(toStopId, cancellationToken);
        if (toStop is null)
        {
            errors.Add($"Destination transport stop {toStopId} was not found.");
        }

        return errors;
    }

    private async Task<bool> HasDuplicateConnectionAsync(
        long fromStopId,
        long toStopId,
        bool isBidirectional,
        long? existingConnectionId,
        CancellationToken cancellationToken)
    {
        var exactConnection = await _transferConnectionRepository.GetActiveByStopsAsync(
            fromStopId,
            toStopId,
            cancellationToken);
        if (IsDifferentConnection(exactConnection, existingConnectionId))
        {
            return true;
        }

        var reverseConnection = await _transferConnectionRepository.GetActiveByStopsAsync(
            toStopId,
            fromStopId,
            cancellationToken);

        return IsDifferentConnection(reverseConnection, existingConnectionId) &&
            (isBidirectional || reverseConnection!.IsBidirectional);
    }

    private static bool IsDifferentConnection(
        TransferConnection? transferConnection,
        long? existingConnectionId) =>
        transferConnection is not null &&
        transferConnection.TransferConnectionId != existingConnectionId;

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
