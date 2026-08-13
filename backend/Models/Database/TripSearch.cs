using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class TripSearch
{
    public Guid TripSearchId { get; set; }

    public Guid? UserId { get; set; }

    public string? OriginName { get; set; }

    public double OriginLatitude { get; set; }

    public double OriginLongitude { get; set; }

    public string? DestinationName { get; set; }

    public double DestinationLatitude { get; set; }

    public double DestinationLongitude { get; set; }

    public decimal? Budget { get; set; }

    public int PassengerCount { get; set; }

    public string? Preference { get; set; }

    public DateTime RequestedAt { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<RouteRecommendation> RouteRecommendations { get; set; } = new List<RouteRecommendation>();

    public virtual UserProfile? User { get; set; }
}
