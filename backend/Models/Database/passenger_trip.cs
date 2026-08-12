using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class passenger_trip
{
    public Guid passenger_trip_id { get; set; }

    public Guid user_id { get; set; }

    public Guid recommendation_id { get; set; }

    public int current_leg_order { get; set; }

    public string status { get; set; } = null!;

    public DateTime? started_at { get; set; }

    public DateTime? completed_at { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual route_recommendation recommendation { get; set; } = null!;

    public virtual ICollection<trip_alert> trip_alerts { get; set; } = new List<trip_alert>();

    public virtual user_profile user { get; set; } = null!;
}
