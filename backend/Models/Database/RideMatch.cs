using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RideMatch
{
    public Guid MatchId { get; set; }

    public Guid RequestId { get; set; }

    public Guid DriverId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid? VehicleId { get; set; }

    public decimal? PickupDistanceMeters { get; set; }

    public decimal? DetourDistanceMeters { get; set; }

    public decimal? EstimatedPickupMinutes { get; set; }

    public decimal? EstimatedTripMinutes { get; set; }

    public decimal? EstimatedFare { get; set; }

    public decimal? MatchScore { get; set; }

    public string Status { get; set; } = null!;

    public DateTime OfferedAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Driver Driver { get; set; } = null!;

    public virtual PassengerRideRequest Request { get; set; } = null!;

    public virtual DriverAvailabilitySession? Session { get; set; }

    public virtual DriverVehicle? Vehicle { get; set; }
}
