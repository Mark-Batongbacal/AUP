using System;

namespace backend.Models.Database;

public partial class FavoriteTrip
{
    public Guid FavoriteTripId { get; set; }

    public Guid UserId { get; set; }

    public Guid RecommendationId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual RouteRecommendation Recommendation { get; set; } = null!;

    public virtual UserProfile User { get; set; } = null!;
}
