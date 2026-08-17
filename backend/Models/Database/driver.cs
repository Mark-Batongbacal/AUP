using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class Driver
{
    public Guid DriverId { get; set; }

    public Guid UserId { get; set; }

    public string? LicenseNumber { get; set; }

    public string VerificationStatus { get; set; } = null!;

    public long? HomeTerminalId { get; set; }

    public decimal? AverageRating { get; set; }

    public int RatingCount { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<DriverAvailabilitySession> DriverAvailabilitySessions { get; set; } = new List<DriverAvailabilitySession>();

    public virtual ICollection<DriverLocation> DriverLocations { get; set; } = new List<DriverLocation>();

    public virtual DriverLocation? DriverLocation { get; set; }

    public virtual ICollection<DriverVehicle> DriverVehicles { get; set; } = new List<DriverVehicle>();

    public virtual TransportStop? HomeTerminal { get; set; }

    public virtual ICollection<RideMatch> RideMatches { get; set; } = new List<RideMatch>();

    public virtual UserProfile User { get; set; } = null!;
}
