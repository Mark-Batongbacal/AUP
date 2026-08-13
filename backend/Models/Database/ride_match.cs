using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class ride_match
{
    public Guid match_id { get; set; }

    public Guid request_id { get; set; }

    public Guid driver_id { get; set; }

    public Guid? session_id { get; set; }

    public Guid? vehicle_id { get; set; }

    public decimal? pickup_distance_meters { get; set; }

    public decimal? detour_distance_meters { get; set; }

    public decimal? estimated_pickup_minutes { get; set; }

    public decimal? estimated_trip_minutes { get; set; }

    public decimal? estimated_fare { get; set; }

    public decimal? match_score { get; set; }

    public string status { get; set; } = null!;

    public DateTime offered_at { get; set; }

    public DateTime? accepted_at { get; set; }

    public DateTime? completed_at { get; set; }

    public virtual driver driver { get; set; } = null!;

    public virtual passenger_ride_request request { get; set; } = null!;

    public virtual driver_availability_session? session { get; set; }

    public virtual driver_vehicle? vehicle { get; set; }
}
