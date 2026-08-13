using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class PassengerTrip
{
    public Guid PassengerTripId { get; set; }

    public Guid UserId { get; set; }

    public Guid RecommendationId { get; set; }

    public int CurrentLegOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual RouteRecommendation Recommendation { get; set; } = null!;

    public virtual ICollection<TripAlert> TripAlerts { get; set; } = new List<TripAlert>();

    public virtual UserProfile User { get; set; } = null!;
}
