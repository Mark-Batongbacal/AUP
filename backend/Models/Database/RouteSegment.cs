using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RouteSegment
{
    public long SegmentId { get; set; }

    public long RouteId { get; set; }

    public long FromStopId { get; set; }

    public long ToStopId { get; set; }

    public long FromRouteStopId { get; set; }

    public long ToRouteStopId { get; set; }

    public int SegmentOrder { get; set; }

    public int? DistanceMeters { get; set; }

    public int? EstimatedDurationSeconds { get; set; }

    public decimal? SegmentFare { get; set; }

    public bool IsBidirectional { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual TransportStop FromStop { get; set; } = null!;

    public virtual RouteStop FromRouteStop { get; set; } = null!;

    public virtual TransportRoute Route { get; set; } = null!;

    public virtual TransportStop ToStop { get; set; } = null!;

    public virtual RouteStop ToRouteStop { get; set; } = null!;
}
