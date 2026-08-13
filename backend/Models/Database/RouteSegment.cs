using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RouteSegment
{
    public long SegmentId { get; set; }

    public Guid RouteId { get; set; }

    public Guid FromStopId { get; set; }

    public Guid ToStopId { get; set; }

    public int SegmentOrder { get; set; }

    public decimal DistanceMeters { get; set; }

    public decimal EstimatedMinutes { get; set; }

    public decimal EstimatedFare { get; set; }

    public bool IsBidirectional { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual TransportStop FromStop { get; set; } = null!;

    public virtual TransportRoute Route { get; set; } = null!;

    public virtual TransportStop ToStop { get; set; } = null!;
}
