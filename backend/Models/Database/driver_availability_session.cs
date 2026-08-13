using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class driver_availability_session
{
    public Guid session_id { get; set; }

    public Guid driver_id { get; set; }

    public Guid? vehicle_id { get; set; }

    public Guid? destination_stop_id { get; set; }

    public string? destination_name { get; set; }

    public double? destination_latitude { get; set; }

    public double? destination_longitude { get; set; }

    public int available_seats { get; set; }

    public decimal maximum_detour_meters { get; set; }

    public string status { get; set; } = null!;

    public DateTime started_at { get; set; }

    public DateTime? ended_at { get; set; }

    public virtual transport_stop? destination_stop { get; set; }

    public virtual driver driver { get; set; } = null!;

    public virtual ICollection<ride_match> ride_matches { get; set; } = new List<ride_match>();

    public virtual driver_vehicle? vehicle { get; set; }
}
