using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class transport_route
{
    public Guid route_id { get; set; }

    public string route_code { get; set; } = null!;

    public string route_name { get; set; } = null!;

    public short transport_mode_id { get; set; }

    public Guid? start_stop_id { get; set; }

    public Guid? end_stop_id { get; set; }

    public string? route_description { get; set; }

    public decimal? base_fare { get; set; }

    public int? estimated_total_minutes { get; set; }

    public TimeOnly? service_start_time { get; set; }

    public TimeOnly? service_end_time { get; set; }

    public int? average_headway_minutes { get; set; }

    public bool operates_monday { get; set; }

    public bool operates_tuesday { get; set; }

    public bool operates_wednesday { get; set; }

    public bool operates_thursday { get; set; }

    public bool operates_friday { get; set; }

    public bool operates_saturday { get; set; }

    public bool operates_sunday { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual transport_stop? end_stop { get; set; }

    public virtual ICollection<fare_rule> fare_rules { get; set; } = new List<fare_rule>();

    public virtual ICollection<recommendation_leg> recommendation_legs { get; set; } = new List<recommendation_leg>();

    public virtual ICollection<route_segment> route_segments { get; set; } = new List<route_segment>();

    public virtual ICollection<route_stop> route_stops { get; set; } = new List<route_stop>();

    public virtual transport_stop? start_stop { get; set; }

    public virtual transport_mode transport_mode { get; set; } = null!;
}
