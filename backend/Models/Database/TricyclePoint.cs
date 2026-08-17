using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class TricyclePoint
{
    public long TricyclePointId { get; set; }

    public long? StopId { get; set; }

    public string PointCode { get; set; } = null!;

    public string PointName { get; set; } = null!;

    public string? Description { get; set; }

    public string? Address { get; set; }

    public string? OperatorName { get; set; }

    public double CenterLatitude { get; set; }

    public double CenterLongitude { get; set; }

    public int RadiusMeters { get; set; }

    public decimal? BaseFare { get; set; }

    public decimal? FarePerKilometer { get; set; }

    public int? AverageWaitingTimeSeconds { get; set; }

    public TimeOnly? ServiceStartTime { get; set; }

    public TimeOnly? ServiceEndTime { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<DriverAvailabilitySession> DriverAvailabilitySessions { get; set; } = new List<DriverAvailabilitySession>();

    public virtual ICollection<DriverVehicle> DriverVehicles { get; set; } = new List<DriverVehicle>();

    public virtual ICollection<PassengerRideRequest> PassengerRideRequests { get; set; } = new List<PassengerRideRequest>();

    public virtual TransportStop? Stop { get; set; }
}
