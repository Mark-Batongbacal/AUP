using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class driver_vehicle
{
    public Guid vehicle_id { get; set; }

    public Guid driver_id { get; set; }

    public short transport_mode_id { get; set; }

    public string? plate_number { get; set; }

    public string? body_number { get; set; }

    public string? color { get; set; }

    public int capacity { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public virtual driver driver { get; set; } = null!;

    public virtual ICollection<driver_availability_session> driver_availability_sessions { get; set; } = new List<driver_availability_session>();

    public virtual ICollection<ride_match> ride_matches { get; set; } = new List<ride_match>();

    public virtual transport_mode transport_mode { get; set; } = null!;
}
