using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class DriverAvailabilitySession
{
    public Guid SessionId { get; set; }

    public Guid DriverId { get; set; }

    public Guid? VehicleId { get; set; }

    public Guid? DestinationStopId { get; set; }

    public string? DestinationName { get; set; }

    public double? DestinationLatitude { get; set; }

    public double? DestinationLongitude { get; set; }

    public int AvailableSeats { get; set; }

    public decimal MaximumDetourMeters { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual TransportStop? DestinationStop { get; set; }

    public virtual Driver Driver { get; set; } = null!;

    public virtual ICollection<RideMatch> RideMatches { get; set; } = new List<RideMatch>();

    public virtual DriverVehicle? Vehicle { get; set; }
}
