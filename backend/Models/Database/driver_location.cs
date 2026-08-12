using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class driver_location
{
    public Guid driver_id { get; set; }

    public double latitude { get; set; }

    public double longitude { get; set; }

    public decimal? heading_degrees { get; set; }

    public decimal? speed_kph { get; set; }

    public decimal? accuracy_meters { get; set; }

    public DateTime updated_at { get; set; }

    public virtual driver driver { get; set; } = null!;
}
