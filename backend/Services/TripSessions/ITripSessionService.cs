using backend.Models.Database;

namespace backend.Services.TripSessions;

public interface ITripSessionService
{
    Task<TripSessionOperation> CreateAsync(Guid userId, CreateTripSessionRequest request, CancellationToken cancellationToken = default);
    Task<TripSessionOperation> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<TripSessionOperation> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TripSessionOperation> StartAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<TripSessionOperation> CancelAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<TripSessionOperation> ConfirmBoardingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<TripSessionOperation> ConfirmAlightingAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<TripSessionOperation> ResolveAlightStatusAsync(
        Guid userId,
        Guid sessionId,
        bool alreadyOff,
        CancellationToken cancellationToken = default);
}

public sealed record CreateTripSessionRequest(Guid RecommendationId);
public sealed record TripSessionOperation(TripSession? Session, string? Error = null)
{
    public bool Succeeded => Session is not null;
}
