using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class transport_stop
{
    public Guid stop_id { get; set; }

    public string? stop_code { get; set; }

    public string name { get; set; } = null!;

    public string? description { get; set; }

    public string stop_type { get; set; } = null!;

    public string? address { get; set; }

    public double latitude { get; set; }

    public double longitude { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual ICollection<driver_availability_session> driver_availability_sessions { get; set; } = new List<driver_availability_session>();

    public virtual ICollection<driver> drivers { get; set; } = new List<driver>();

    public virtual ICollection<recommendation_leg> recommendation_legfrom_stops { get; set; } = new List<recommendation_leg>();

    public virtual ICollection<recommendation_leg> recommendation_legto_stops { get; set; } = new List<recommendation_leg>();

    public virtual ICollection<route_segment> route_segmentfrom_stops { get; set; } = new List<route_segment>();

    public virtual ICollection<route_segment> route_segmentto_stops { get; set; } = new List<route_segment>();

    public virtual ICollection<route_stop> route_stops { get; set; } = new List<route_stop>();

    public virtual ICollection<transport_route> transport_routeend_stops { get; set; } = new List<transport_route>();

    public virtual ICollection<transport_route> transport_routestart_stops { get; set; } = new List<transport_route>();

    public virtual ICollection<trip_alert> trip_alerts { get; set; } = new List<trip_alert>();
}
