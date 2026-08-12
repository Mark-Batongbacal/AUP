using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class fare_rule
{
    public Guid fare_rule_id { get; set; }

    public short transport_mode_id { get; set; }

    public Guid? route_id { get; set; }

    public string rule_name { get; set; } = null!;

    public decimal base_fare { get; set; }

    public decimal? base_distance_km { get; set; }

    public decimal? additional_fare_per_km { get; set; }

    public decimal? minimum_fare { get; set; }

    public decimal? maximum_fare { get; set; }

    public DateOnly effective_from { get; set; }

    public DateOnly? effective_to { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public virtual transport_route? route { get; set; }

    public virtual transport_mode transport_mode { get; set; } = null!;
}
