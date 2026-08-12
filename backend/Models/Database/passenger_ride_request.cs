using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class passenger_ride_request
{
    public Guid request_id { get; set; }

    public Guid passenger_user_id { get; set; }

    public short? transport_mode_id { get; set; }

    public string? pickup_name { get; set; }

    public double pickup_latitude { get; set; }

    public double pickup_longitude { get; set; }

    public string? dropoff_name { get; set; }

    public double dropoff_latitude { get; set; }

    public double dropoff_longitude { get; set; }

    public int passenger_count { get; set; }

    public decimal? max_budget { get; set; }

    public string status { get; set; } = null!;

    public DateTime requested_at { get; set; }

    public DateTime? expires_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual user_profile passenger_user { get; set; } = null!;

    public virtual ICollection<ride_match> ride_matches { get; set; } = new List<ride_match>();

    public virtual transport_mode? transport_mode { get; set; }
}
