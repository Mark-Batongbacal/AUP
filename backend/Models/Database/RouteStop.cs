using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RouteStop
{
    public long RouteStopId { get; set; }

    public long RouteId { get; set; }

    public long StopId { get; set; }

    public int StopOrder { get; set; }

    public int? EstimatedMinutesFromStart { get; set; }

    public int? DistanceFromRouteStartMeters { get; set; }

    public string? Instructions { get; set; }

    public bool CanBoard { get; set; }

    public bool CanAlight { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TransportRoute Route { get; set; } = null!;

    public virtual TransportStop Stop { get; set; } = null!;

    public virtual ICollection<RouteSegment> RouteSegmentFromRouteStops { get; set; } = new List<RouteSegment>();

    public virtual ICollection<RouteSegment> RouteSegmentToRouteStops { get; set; } = new List<RouteSegment>();
}
