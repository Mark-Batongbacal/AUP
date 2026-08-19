namespace backend.Models.Database;

public enum TripNavigationState
{
    Planned,
    Starting,
    WalkingToPickup,
    ApproachingBoardPoint,
    WaitingToBoard,
    OnJeepney,
    OnTricycle,
    ApproachingAlightPoint,
    Transferring,
    WalkingToDestination,
    OffRoute,
    Rerouting,
    Arrived,
    Cancelled
}

public sealed class TripSession
{
    public Guid TripSessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid RecommendationId { get; set; }
    public double OriginLatitude { get; set; }
    public double OriginLongitude { get; set; }
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public string? DestinationName { get; set; }
    public int CurrentLegIndex { get; set; }
    public TripNavigationState CurrentNavigationState { get; set; }
    public double CurrentProgressMeters { get; set; }
    public double? CurrentRouteProgressMeters { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastLocationAt { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public double? LastAccuracyMeters { get; set; }
    public int ConsecutiveStateConfirmationSamples { get; set; }
    public int ConsecutiveOffRouteSamples { get; set; }
    public DateTime? OffRouteSuspectedAt { get; set; }
    public string? LastRerouteReason { get; set; }
    public string? LastNavigationStatus { get; set; }
    public string? LastSpeechEventKey { get; set; }
    public string? LastSpokenInstruction { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal? OriginalBudget { get; set; }
    public string? OriginalPreference { get; set; }
    public DateTime? LastRerouteAt { get; set; }
    public int RerouteCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public UserProfile User { get; set; } = null!;
    public RouteRecommendation Recommendation { get; set; } = null!;
    public ICollection<NavigationInstruction> NavigationInstructions { get; set; } = [];
    public ICollection<TripLandmarkCandidate> CachedLandmarks { get; set; } = [];
}
