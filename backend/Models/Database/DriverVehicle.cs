using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class DriverVehicle
{
    public Guid VehicleId { get; set; }

    public Guid DriverId { get; set; }

    public int TransportModeId { get; set; }

    public long? TricyclePointId { get; set; }

    public string VehicleType { get; set; } = null!;

    public string? PlateNumber { get; set; }

    public string? BodyNumber { get; set; }

    public string? Color { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Driver Driver { get; set; } = null!;

    public virtual ICollection<DriverAvailabilitySession> DriverAvailabilitySessions { get; set; } = new List<DriverAvailabilitySession>();

    public virtual ICollection<RideMatch> RideMatches { get; set; } = new List<RideMatch>();

    public virtual TransportMode TransportMode { get; set; } = null!;

    public virtual TricyclePoint? TricyclePoint { get; set; }
}
