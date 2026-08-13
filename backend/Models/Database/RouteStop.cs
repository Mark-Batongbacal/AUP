using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RouteStop
{
    public Guid RouteStopId { get; set; }

    public Guid RouteId { get; set; }

    public Guid StopId { get; set; }

    public int StopOrder { get; set; }

    public int? EstimatedMinutesFromStart { get; set; }

    public bool CanBoard { get; set; }

    public bool CanAlight { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TransportRoute Route { get; set; } = null!;

    public virtual TransportStop Stop { get; set; } = null!;
}
