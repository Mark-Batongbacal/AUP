using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class recommendation_leg
{
    public Guid leg_id { get; set; }

    public Guid recommendation_id { get; set; }

    public int leg_order { get; set; }

    public short transport_mode_id { get; set; }

    public Guid? route_id { get; set; }

    public Guid? from_stop_id { get; set; }

    public Guid? to_stop_id { get; set; }

    public string? from_name { get; set; }

    public string? to_name { get; set; }

    public double? start_latitude { get; set; }

    public double? start_longitude { get; set; }

    public double? end_latitude { get; set; }

    public double? end_longitude { get; set; }

    public decimal? distance_meters { get; set; }

    public decimal estimated_minutes { get; set; }

    public decimal estimated_fare { get; set; }

    public string? instructions { get; set; }

    public DateTime created_at { get; set; }

    public virtual transport_stop? from_stop { get; set; }

    public virtual route_recommendation recommendation { get; set; } = null!;

    public virtual transport_route? route { get; set; }

    public virtual transport_stop? to_stop { get; set; }

    public virtual transport_mode transport_mode { get; set; } = null!;

    public virtual ICollection<trip_alert> trip_alerts { get; set; } = new List<trip_alert>();
}
