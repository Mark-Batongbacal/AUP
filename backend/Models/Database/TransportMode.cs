using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class TransportMode
{
    public int TransportModeId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsMotorized { get; set; }

    public bool AllowsLiveDriver { get; set; }

    public string? IconName { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<DriverVehicle> DriverVehicles { get; set; } = new List<DriverVehicle>();

    public virtual ICollection<FareRule> FareRules { get; set; } = new List<FareRule>();

    public virtual ICollection<PassengerRideRequest> PassengerRideRequests { get; set; } = new List<PassengerRideRequest>();

    public virtual ICollection<RecommendationLeg> RecommendationLegs { get; set; } = new List<RecommendationLeg>();

    public virtual ICollection<TransportRoute> TransportRoutes { get; set; } = new List<TransportRoute>();
}
