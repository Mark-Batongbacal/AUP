using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class route_segment
{
    public long segment_id { get; set; }

    public Guid route_id { get; set; }

    public Guid from_stop_id { get; set; }

    public Guid to_stop_id { get; set; }

    public int segment_order { get; set; }

    public decimal distance_meters { get; set; }

    public decimal estimated_minutes { get; set; }

    public decimal estimated_fare { get; set; }

    public bool is_bidirectional { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual transport_stop from_stop { get; set; } = null!;

    public virtual transport_route route { get; set; } = null!;

    public virtual transport_stop to_stop { get; set; } = null!;
}
