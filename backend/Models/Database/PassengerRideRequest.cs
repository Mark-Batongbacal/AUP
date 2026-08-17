using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class PassengerRideRequest
{
    public Guid RequestId { get; set; }

    public Guid PassengerUserId { get; set; }

    public int? TransportModeId { get; set; }

    public long? TricyclePointId { get; set; }

    public string? PickupName { get; set; }

    public double PickupLatitude { get; set; }

    public double PickupLongitude { get; set; }

    public string? DropoffName { get; set; }

    public double DropoffLatitude { get; set; }

    public double DropoffLongitude { get; set; }

    public double DestinationLatitude { get; set; }

    public double DestinationLongitude { get; set; }

    public int PassengerCount { get; set; }

    public decimal? MaxBudget { get; set; }

    public decimal? EstimatedFare { get; set; }

    public int? EstimatedDistanceMeters { get; set; }

    public int? EstimatedDurationSeconds { get; set; }

    public string Status { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual UserProfile PassengerUser { get; set; } = null!;

    public virtual ICollection<RideMatch> RideMatches { get; set; } = new List<RideMatch>();

    public virtual TransportMode? TransportMode { get; set; }

    public virtual TricyclePoint? TricyclePoint { get; set; }
}
