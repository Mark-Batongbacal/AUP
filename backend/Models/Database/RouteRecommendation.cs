using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class RouteRecommendation
{
    public Guid RecommendationId { get; set; }

    public Guid TripSearchId { get; set; }

    public string RecommendationType { get; set; } = null!;

    public int RankNumber { get; set; }

    public decimal TotalFare { get; set; }

    public decimal TotalMinutes { get; set; }

    public decimal? TotalDistanceMeters { get; set; }

    public decimal WalkingDistanceMeters { get; set; }

    public int TransferCount { get; set; }

    public decimal? RecommendationScore { get; set; }

    public string? Explanation { get; set; }

    public DateTime GeneratedAt { get; set; }

    public virtual ICollection<FavoriteTrip> FavoriteTrips { get; set; } = new List<FavoriteTrip>();

    public virtual ICollection<PassengerTrip> PassengerTrips { get; set; } = new List<PassengerTrip>();

    public virtual ICollection<RecommendationLeg> RecommendationLegs { get; set; } = new List<RecommendationLeg>();

    public virtual TripSearch TripSearch { get; set; } = null!;
    public virtual ICollection<TripSession> TripSessions { get; set; } = new List<TripSession>();
}
