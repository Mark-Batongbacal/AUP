namespace backend.Models.Database;

public sealed class TripLandmarkCandidate
{
    public Guid TripLandmarkCandidateId { get; set; }
    public Guid TripSessionId { get; set; }
    public int LegIndex { get; set; }
    public string ExternalPlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceFromRouteStartMeters { get; set; }
    public double TriggerBeforeMeters { get; set; }
    public double TriggerAfterMeters { get; set; }
    public DateTime CachedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public TripSession TripSession { get; set; } = null!;
}
