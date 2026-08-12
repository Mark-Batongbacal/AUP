using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class driver
{
    public Guid driver_id { get; set; }

    public Guid user_id { get; set; }

    public string? license_number { get; set; }

    public string verification_status { get; set; } = null!;

    public Guid? home_terminal_id { get; set; }

    public decimal? average_rating { get; set; }

    public int rating_count { get; set; }

    public bool is_available { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual ICollection<driver_availability_session> driver_availability_sessions { get; set; } = new List<driver_availability_session>();

    public virtual driver_location? driver_location { get; set; }

    public virtual ICollection<driver_vehicle> driver_vehicles { get; set; } = new List<driver_vehicle>();

    public virtual transport_stop? home_terminal { get; set; }

    public virtual ICollection<ride_match> ride_matches { get; set; } = new List<ride_match>();

    public virtual user_profile user { get; set; } = null!;
}
