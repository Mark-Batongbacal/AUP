using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class transport_mode
{
    public short transport_mode_id { get; set; }

    public string code { get; set; } = null!;

    public string name { get; set; } = null!;

    public bool is_motorized { get; set; }

    public bool allows_live_driver { get; set; }

    public string? icon_name { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public virtual ICollection<driver_vehicle> driver_vehicles { get; set; } = new List<driver_vehicle>();

    public virtual ICollection<fare_rule> fare_rules { get; set; } = new List<fare_rule>();

    public virtual ICollection<passenger_ride_request> passenger_ride_requests { get; set; } = new List<passenger_ride_request>();

    public virtual ICollection<recommendation_leg> recommendation_legs { get; set; } = new List<recommendation_leg>();

    public virtual ICollection<transport_route> transport_routes { get; set; } = new List<transport_route>();
}
