using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RoutePoint
{
    public long RoutePointId { get; set; }

    public long RouteId { get; set; }

    public int PointOrder { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TransportRoute Route { get; set; } = null!;
}
