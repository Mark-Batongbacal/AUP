using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class TripAlert
{
    public Guid AlertId { get; set; }

    public Guid PassengerTripId { get; set; }

    public Guid? LegId { get; set; }

    public Guid? TargetStopId { get; set; }

    public string AlertType { get; set; } = null!;

    public string? Title { get; set; }

    public string Message { get; set; } = null!;

    public decimal? TriggerDistanceMeters { get; set; }

    public bool IsTriggered { get; set; }

    public DateTime? TriggeredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual RecommendationLeg? Leg { get; set; }

    public virtual PassengerTrip PassengerTrip { get; set; } = null!;

    public virtual TransportStop? TargetStop { get; set; }
}
