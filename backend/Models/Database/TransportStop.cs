using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class TransportStop
{
    public long StopId { get; set; }

    public string? StopCode { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string StopType { get; set; } = null!;

    public string? Address { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<DriverAvailabilitySession> DriverAvailabilitySessions { get; set; } = new List<DriverAvailabilitySession>();

    public virtual ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    public virtual ICollection<RecommendationLeg> RecommendationLegsStartingHere { get; set; } = new List<RecommendationLeg>();

    public virtual ICollection<RecommendationLeg> RecommendationLegsEndingHere { get; set; } = new List<RecommendationLeg>();

    public virtual ICollection<RouteSegment> SegmentsStartingHere { get; set; } = new List<RouteSegment>();

    public virtual ICollection<RouteSegment> SegmentsEndingHere { get; set; } = new List<RouteSegment>();

    public virtual ICollection<RouteStop> RouteStops { get; set; } = new List<RouteStop>();

    public virtual ICollection<TransferConnection> TransferConnectionsFromStop { get; set; } = new List<TransferConnection>();

    public virtual ICollection<TransferConnection> TransferConnectionsToStop { get; set; } = new List<TransferConnection>();

    public virtual TricyclePoint? TricyclePoint { get; set; }

    public virtual ICollection<TransportRoute> RoutesEndingHere { get; set; } = new List<TransportRoute>();

    public virtual ICollection<TransportRoute> RoutesStartingHere { get; set; } = new List<TransportRoute>();

    public virtual ICollection<TripAlert> TripAlerts { get; set; } = new List<TripAlert>();
}
