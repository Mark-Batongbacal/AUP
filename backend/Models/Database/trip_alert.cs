using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class trip_alert
{
    public Guid alert_id { get; set; }

    public Guid passenger_trip_id { get; set; }

    public Guid? leg_id { get; set; }

    public Guid? target_stop_id { get; set; }

    public string alert_type { get; set; } = null!;

    public string? title { get; set; }

    public string message { get; set; } = null!;

    public decimal? trigger_distance_meters { get; set; }

    public bool is_triggered { get; set; }

    public DateTime? triggered_at { get; set; }

    public DateTime created_at { get; set; }

    public virtual recommendation_leg? leg { get; set; }

    public virtual passenger_trip passenger_trip { get; set; } = null!;

    public virtual transport_stop? target_stop { get; set; }
}
