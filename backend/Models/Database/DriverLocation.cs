using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class DriverLocation
{
    public long DriverLocationId { get; set; }

    public Guid DriverId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? HeadingDegrees { get; set; }

    public double? SpeedKph { get; set; }

    public double? AccuracyMeters { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Driver Driver { get; set; } = null!;
}
