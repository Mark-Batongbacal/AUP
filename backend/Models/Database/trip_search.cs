using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class trip_search
{
    public Guid trip_search_id { get; set; }

    public Guid? user_id { get; set; }

    public string? origin_name { get; set; }

    public double origin_latitude { get; set; }

    public double origin_longitude { get; set; }

    public string? destination_name { get; set; }

    public double destination_latitude { get; set; }

    public double destination_longitude { get; set; }

    public decimal? budget { get; set; }

    public int passenger_count { get; set; }

    public string? preference { get; set; }

    public DateTime requested_at { get; set; }

    public virtual ICollection<chat_message> chat_messages { get; set; } = new List<chat_message>();

    public virtual ICollection<route_recommendation> route_recommendations { get; set; } = new List<route_recommendation>();

    public virtual user_profile? user { get; set; }
}
