using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class route_recommendation
{
    public Guid recommendation_id { get; set; }

    public Guid trip_search_id { get; set; }

    public string recommendation_type { get; set; } = null!;

    public int rank_number { get; set; }

    public decimal total_fare { get; set; }

    public decimal total_minutes { get; set; }

    public decimal? total_distance_meters { get; set; }

    public decimal walking_distance_meters { get; set; }

    public int transfer_count { get; set; }

    public decimal? recommendation_score { get; set; }

    public string? explanation { get; set; }

    public DateTime generated_at { get; set; }

    public virtual ICollection<passenger_trip> passenger_trips { get; set; } = new List<passenger_trip>();

    public virtual ICollection<recommendation_leg> recommendation_legs { get; set; } = new List<recommendation_leg>();

    public virtual trip_search trip_search { get; set; } = null!;
}
