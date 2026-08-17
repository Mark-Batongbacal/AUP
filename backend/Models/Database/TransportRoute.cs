using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class TransportRoute
{
    public long RouteId { get; set; }

    public string RouteCode { get; set; } = null!;

    public string RouteName { get; set; } = null!;

    public int TransportModeId { get; set; }

    public long? StartStopId { get; set; }

    public long? EndStopId { get; set; }

    public string OriginName { get; set; } = null!;

    public string DestinationName { get; set; } = null!;

    public string? DirectionName { get; set; }

    public string? OperatorName { get; set; }

    public string? RouteDescription { get; set; }

    public string? EncodedPolyline { get; set; }

    public decimal? BaseFare { get; set; }

    public int? EstimatedDistanceMeters { get; set; }

    public int? EstimatedDurationSeconds { get; set; }

    public int? EstimatedTotalMinutes { get; set; }

    public int? AverageWaitingTimeSeconds { get; set; }

    public TimeOnly? ServiceStartTime { get; set; }

    public TimeOnly? ServiceEndTime { get; set; }

    public int? AverageHeadwayMinutes { get; set; }

    public bool OperatesMonday { get; set; }

    public bool OperatesTuesday { get; set; }

    public bool OperatesWednesday { get; set; }

    public bool OperatesThursday { get; set; }

    public bool OperatesFriday { get; set; }

    public bool OperatesSaturday { get; set; }

    public bool OperatesSunday { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual TransportStop? EndStop { get; set; }

    public virtual ICollection<FareRule> FareRules { get; set; } = new List<FareRule>();

    public virtual ICollection<RecommendationLeg> RecommendationLegs { get; set; } = new List<RecommendationLeg>();

    public virtual ICollection<RoutePoint> RoutePoints { get; set; } = new List<RoutePoint>();

    public virtual ICollection<RouteWaypoint> RouteWaypoints { get; set; } = new List<RouteWaypoint>();

    public virtual ICollection<RouteSegment> RouteSegments { get; set; } = new List<RouteSegment>();

    public virtual ICollection<RouteStop> RouteStops { get; set; } = new List<RouteStop>();

    public virtual TransportStop? StartStop { get; set; }

    public virtual TransportMode TransportMode { get; set; } = null!;
}
