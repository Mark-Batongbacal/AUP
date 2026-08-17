using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class TransferConnection
{
    public long TransferConnectionId { get; set; }

    public long FromStopId { get; set; }

    public long ToStopId { get; set; }

    public int? MaximumWalkingDistanceMeters { get; set; }

    public int? EstimatedWalkingTimeSeconds { get; set; }

    public string? Instructions { get; set; }

    public bool IsBidirectional { get; set; }

    public bool IsActive { get; set; }

    public virtual TransportStop FromStop { get; set; } = null!;

    public virtual TransportStop ToStop { get; set; } = null!;
}
