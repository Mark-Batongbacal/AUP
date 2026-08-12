using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class user_profile
{
    public Guid user_id { get; set; }

    public string? first_name { get; set; }

    public string? last_name { get; set; }

    public string? phone_number { get; set; }

    public string role { get; set; } = null!;

    public string? profile_image_url { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual ICollection<chat_conversation> chat_conversations { get; set; } = new List<chat_conversation>();

    public virtual driver? driver { get; set; }

    public virtual ICollection<passenger_ride_request> passenger_ride_requests { get; set; } = new List<passenger_ride_request>();

    public virtual ICollection<passenger_trip> passenger_trips { get; set; } = new List<passenger_trip>();

    public virtual ICollection<trip_search> trip_searches { get; set; } = new List<trip_search>();
}
