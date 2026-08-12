using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class route_stop
{
    public Guid route_stop_id { get; set; }

    public Guid route_id { get; set; }

    public Guid stop_id { get; set; }

    public int stop_order { get; set; }

    public int? estimated_minutes_from_start { get; set; }

    public bool can_board { get; set; }

    public bool can_alight { get; set; }

    public DateTime created_at { get; set; }

    public virtual transport_route route { get; set; } = null!;

    public virtual transport_stop stop { get; set; } = null!;
}
