using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class DriverLocation
{
    public Guid DriverId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public decimal? HeadingDegrees { get; set; }

    public decimal? SpeedKph { get; set; }

    public decimal? AccuracyMeters { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Driver Driver { get; set; } = null!;
}
