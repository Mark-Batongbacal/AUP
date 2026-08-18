using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class UserProfile
{
    public Guid UserId { get; set; }

    public string? ExternalAuthProvider { get; set; }

    public string? ExternalAuthId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = null!;

    public string? ProfileImageUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ChatConversation> ChatConversations { get; set; } = new List<ChatConversation>();

    public virtual Driver? Driver { get; set; }

    public virtual ICollection<PassengerRideRequest> PassengerRideRequests { get; set; } = new List<PassengerRideRequest>();

    public virtual ICollection<PassengerTrip> PassengerTrips { get; set; } = new List<PassengerTrip>();

    public virtual ICollection<TripSearch> TripSearches { get; set; } = new List<TripSearch>();
    public virtual ICollection<TripSession> TripSessions { get; set; } = new List<TripSession>();
}
