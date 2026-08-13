using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class PassengerRideRequest
{
    public Guid RequestId { get; set; }

    public Guid PassengerUserId { get; set; }

    public short? TransportModeId { get; set; }

    public string? PickupName { get; set; }

    public double PickupLatitude { get; set; }

    public double PickupLongitude { get; set; }

    public string? DropoffName { get; set; }

    public double DropoffLatitude { get; set; }

    public double DropoffLongitude { get; set; }

    public int PassengerCount { get; set; }

    public decimal? MaxBudget { get; set; }

    public string Status { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual UserProfile PassengerUser { get; set; } = null!;

    public virtual ICollection<RideMatch> RideMatches { get; set; } = new List<RideMatch>();

    public virtual TransportMode? TransportMode { get; set; }
}
