using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RecommendationLeg
{
    public Guid LegId { get; set; }

    public Guid RecommendationId { get; set; }

    public int LegOrder { get; set; }

    public int TransportModeId { get; set; }

    public long? RouteId { get; set; }

    public long? FromStopId { get; set; }

    public long? ToStopId { get; set; }

    public string? FromName { get; set; }

    public string? ToName { get; set; }

    public double? StartLatitude { get; set; }

    public double? StartLongitude { get; set; }

    public double? EndLatitude { get; set; }

    public double? EndLongitude { get; set; }

    public double? StartRouteProgressMeters { get; set; }

    public double? EndRouteProgressMeters { get; set; }

    public bool StartsAlreadyOnboard { get; set; }

    public decimal? DistanceMeters { get; set; }

    public decimal EstimatedMinutes { get; set; }

    public decimal EstimatedFare { get; set; }

    public string? Instructions { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TransportStop? FromStop { get; set; }

    public virtual RouteRecommendation Recommendation { get; set; } = null!;

    public virtual TransportRoute? Route { get; set; }

    public virtual TransportStop? ToStop { get; set; }

    public virtual TransportMode TransportMode { get; set; } = null!;

    public virtual ICollection<TripAlert> TripAlerts { get; set; } = new List<TripAlert>();
}
