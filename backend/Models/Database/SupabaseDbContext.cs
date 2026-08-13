using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace backend.Models.Database;

public partial class SupabaseDbContext : DbContext
{
    public SupabaseDbContext(DbContextOptions<SupabaseDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChatConversation> ChatConversations { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<Driver> Drivers { get; set; }

    public virtual DbSet<DriverAvailabilitySession> DriverAvailabilitySessions { get; set; }

    public virtual DbSet<DriverLocation> DriverLocations { get; set; }

    public virtual DbSet<DriverVehicle> DriverVehicles { get; set; }

    public virtual DbSet<FareRule> FareRules { get; set; }

    public virtual DbSet<PassengerRideRequest> PassengerRideRequests { get; set; }

    public virtual DbSet<PassengerTrip> PassengerTrips { get; set; }

    public virtual DbSet<RecommendationLeg> RecommendationLegs { get; set; }

    public virtual DbSet<RideMatch> RideMatches { get; set; }

    public virtual DbSet<RouteRecommendation> RouteRecommendations { get; set; }

    public virtual DbSet<RouteSegment> RouteSegments { get; set; }

    public virtual DbSet<RouteStop> RouteStops { get; set; }

    public virtual DbSet<TransportMode> TransportModes { get; set; }

    public virtual DbSet<TransportRoute> TransportRoutes { get; set; }

    public virtual DbSet<TransportStop> TransportStops { get; set; }

    public virtual DbSet<TripAlert> TripAlerts { get; set; }

    public virtual DbSet<TripSearch> TripSearches { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in", "like", "ilike", "is", "match", "imatch", "isdistinct" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("gis", "postgis")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("chat_conversations_pkey");

            entity.HasIndex(e => e.UserId, "idx_chat_conversations_user");

            entity.Property(e => e.ConversationId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithMany(p => p.ChatConversations)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("chat_conversations_user_id_fkey");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("chat_messages_pkey");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "idx_chat_messages_conversation");

            entity.Property(e => e.MessageId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ExtractedBudget).HasPrecision(10, 2);
            entity.Property(e => e.Sender).HasMaxLength(20);

            entity.HasOne(d => d.Conversation).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("chat_messages_conversation_id_fkey");

            entity.HasOne(d => d.TripSearch).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.TripSearchId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("chat_messages_trip_search_id_fkey");
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("drivers_pkey");

            entity.HasIndex(e => e.UserId, "drivers_user_id_key").IsUnique();

            entity.Property(e => e.DriverId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AverageRating).HasPrecision(3, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsAvailable).HasDefaultValue(false);
            entity.Property(e => e.LicenseNumber).HasMaxLength(100);
            entity.Property(e => e.RatingCount).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.VerificationStatus)
                .HasMaxLength(30)
                .HasDefaultValueSql("'PENDING'::character varying");

            entity.HasOne(d => d.HomeTerminal).WithMany(p => p.Drivers)
                .HasForeignKey(d => d.HomeTerminalId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("drivers_home_terminal_id_fkey");

            entity.HasOne(d => d.User).WithOne(p => p.Driver)
                .HasForeignKey<Driver>(d => d.UserId)
                .HasConstraintName("drivers_user_id_fkey");
        });

        modelBuilder.Entity<DriverAvailabilitySession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("driver_availability_sessions_pkey");

            entity.HasIndex(e => e.DriverId, "idx_driver_sessions_driver");

            entity.Property(e => e.SessionId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AvailableSeats).HasDefaultValue(1);
            entity.Property(e => e.DestinationName).HasMaxLength(250);
            entity.Property(e => e.MaximumDetourMeters)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("1000");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'AVAILABLE'::character varying");

            entity.HasOne(d => d.DestinationStop).WithMany(p => p.DriverAvailabilitySessions)
                .HasForeignKey(d => d.DestinationStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("driver_availability_sessions_destination_stop_id_fkey");

            entity.HasOne(d => d.Driver).WithMany(p => p.DriverAvailabilitySessions)
                .HasForeignKey(d => d.DriverId)
                .HasConstraintName("driver_availability_sessions_driver_id_fkey");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.DriverAvailabilitySessions)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("driver_availability_sessions_vehicle_id_fkey");
        });

        modelBuilder.Entity<DriverLocation>(entity =>
        {
            entity.HasKey(e => e.DriverId).HasName("driver_locations_pkey");

            entity.Property(e => e.DriverId).ValueGeneratedNever();
            entity.Property(e => e.AccuracyMeters).HasPrecision(8, 2);
            entity.Property(e => e.HeadingDegrees).HasPrecision(6, 2);
            entity.Property(e => e.SpeedKph).HasPrecision(8, 2);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Driver).WithOne(p => p.DriverLocation)
                .HasForeignKey<DriverLocation>(d => d.DriverId)
                .HasConstraintName("driver_locations_driver_id_fkey");
        });

        modelBuilder.Entity<DriverVehicle>(entity =>
        {
            entity.HasKey(e => e.VehicleId).HasName("driver_vehicles_pkey");

            entity.HasIndex(e => new { e.DriverId, e.PlateNumber }, "driver_vehicles_driver_id_plate_number_key").IsUnique();

            entity.Property(e => e.VehicleId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.BodyNumber).HasMaxLength(50);
            entity.Property(e => e.Capacity).HasDefaultValue(1);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PlateNumber).HasMaxLength(50);

            entity.HasOne(d => d.Driver).WithMany(p => p.DriverVehicles)
                .HasForeignKey(d => d.DriverId)
                .HasConstraintName("driver_vehicles_driver_id_fkey");

            entity.HasOne(d => d.TransportMode).WithMany(p => p.DriverVehicles)
                .HasForeignKey(d => d.TransportModeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("driver_vehicles_transport_mode_id_fkey");
        });

        modelBuilder.Entity<FareRule>(entity =>
        {
            entity.HasKey(e => e.FareRuleId).HasName("fare_rules_pkey");

            entity.HasIndex(e => e.RouteId, "idx_fare_rules_route");

            entity.Property(e => e.FareRuleId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AdditionalFarePerKm).HasPrecision(10, 2);
            entity.Property(e => e.BaseDistanceKm).HasPrecision(10, 2);
            entity.Property(e => e.BaseFare).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.EffectiveFrom).HasDefaultValueSql("CURRENT_DATE");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaximumFare).HasPrecision(10, 2);
            entity.Property(e => e.MinimumFare).HasPrecision(10, 2);
            entity.Property(e => e.RuleName).HasMaxLength(150);

            entity.HasOne(d => d.Route).WithMany(p => p.FareRules)
                .HasForeignKey(d => d.RouteId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fare_rules_route_id_fkey");

            entity.HasOne(d => d.TransportMode).WithMany(p => p.FareRules)
                .HasForeignKey(d => d.TransportModeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fare_rules_transport_mode_id_fkey");
        });

        modelBuilder.Entity<PassengerRideRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("passenger_ride_requests_pkey");

            entity.HasIndex(e => e.PassengerUserId, "idx_passenger_requests_passenger");

            entity.HasIndex(e => e.Status, "idx_passenger_requests_status");

            entity.Property(e => e.RequestId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.DropoffName).HasMaxLength(250);
            entity.Property(e => e.MaxBudget).HasPrecision(10, 2);
            entity.Property(e => e.PassengerCount).HasDefaultValue(1);
            entity.Property(e => e.PickupName).HasMaxLength(250);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'SEARCHING'::character varying");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.PassengerUser).WithMany(p => p.PassengerRideRequests)
                .HasForeignKey(d => d.PassengerUserId)
                .HasConstraintName("passenger_ride_requests_passenger_user_id_fkey");

            entity.HasOne(d => d.TransportMode).WithMany(p => p.PassengerRideRequests)
                .HasForeignKey(d => d.TransportModeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("passenger_ride_requests_transport_mode_id_fkey");
        });

        modelBuilder.Entity<PassengerTrip>(entity =>
        {
            entity.HasKey(e => e.PassengerTripId).HasName("passenger_trips_pkey");

            entity.HasIndex(e => e.UserId, "idx_passenger_trips_user");

            entity.Property(e => e.PassengerTripId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrentLegOrder).HasDefaultValue(1);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'PLANNED'::character varying");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Recommendation).WithMany(p => p.PassengerTrips)
                .HasForeignKey(d => d.RecommendationId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("passenger_trips_recommendation_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.PassengerTrips)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("passenger_trips_user_id_fkey");
        });

        modelBuilder.Entity<RecommendationLeg>(entity =>
        {
            entity.HasKey(e => e.LegId).HasName("recommendation_legs_pkey");

            entity.HasIndex(e => new { e.RecommendationId, e.LegOrder }, "idx_recommendation_legs_recommendation");

            entity.HasIndex(e => new { e.RecommendationId, e.LegOrder }, "recommendation_legs_recommendation_id_leg_order_key").IsUnique();

            entity.Property(e => e.LegId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DistanceMeters).HasPrecision(12, 2);
            entity.Property(e => e.EstimatedFare).HasPrecision(10, 2);
            entity.Property(e => e.EstimatedMinutes).HasPrecision(10, 2);
            entity.Property(e => e.FromName).HasMaxLength(250);
            entity.Property(e => e.ToName).HasMaxLength(250);

            entity.HasOne(d => d.FromStop).WithMany(p => p.RecommendationLegsStartingHere)
                .HasForeignKey(d => d.FromStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("recommendation_legs_from_stop_id_fkey");

            entity.HasOne(d => d.Recommendation).WithMany(p => p.RecommendationLegs)
                .HasForeignKey(d => d.RecommendationId)
                .HasConstraintName("recommendation_legs_recommendation_id_fkey");

            entity.HasOne(d => d.Route).WithMany(p => p.RecommendationLegs)
                .HasForeignKey(d => d.RouteId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("recommendation_legs_route_id_fkey");

            entity.HasOne(d => d.ToStop).WithMany(p => p.RecommendationLegsEndingHere)
                .HasForeignKey(d => d.ToStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("recommendation_legs_to_stop_id_fkey");

            entity.HasOne(d => d.TransportMode).WithMany(p => p.RecommendationLegs)
                .HasForeignKey(d => d.TransportModeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("recommendation_legs_transport_mode_id_fkey");
        });

        modelBuilder.Entity<RideMatch>(entity =>
        {
            entity.HasKey(e => e.MatchId).HasName("ride_matches_pkey");

            entity.HasIndex(e => e.DriverId, "idx_ride_matches_driver");

            entity.HasIndex(e => e.RequestId, "idx_ride_matches_request");

            entity.HasIndex(e => new { e.RequestId, e.DriverId, e.SessionId }, "ride_matches_request_id_driver_id_session_id_key").IsUnique();

            entity.Property(e => e.MatchId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.DetourDistanceMeters).HasPrecision(10, 2);
            entity.Property(e => e.EstimatedFare).HasPrecision(10, 2);
            entity.Property(e => e.EstimatedPickupMinutes).HasPrecision(10, 2);
            entity.Property(e => e.EstimatedTripMinutes).HasPrecision(10, 2);
            entity.Property(e => e.MatchScore).HasPrecision(12, 6);
            entity.Property(e => e.OfferedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PickupDistanceMeters).HasPrecision(10, 2);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'OFFERED'::character varying");

            entity.HasOne(d => d.Driver).WithMany(p => p.RideMatches)
                .HasForeignKey(d => d.DriverId)
                .HasConstraintName("ride_matches_driver_id_fkey");

            entity.HasOne(d => d.Request).WithMany(p => p.RideMatches)
                .HasForeignKey(d => d.RequestId)
                .HasConstraintName("ride_matches_request_id_fkey");

            entity.HasOne(d => d.Session).WithMany(p => p.RideMatches)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("ride_matches_session_id_fkey");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.RideMatches)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("ride_matches_vehicle_id_fkey");
        });

        modelBuilder.Entity<RouteRecommendation>(entity =>
        {
            entity.HasKey(e => e.RecommendationId).HasName("route_recommendations_pkey");

            entity.HasIndex(e => e.TripSearchId, "idx_route_recommendations_search");

            entity.HasIndex(e => new { e.TripSearchId, e.RecommendationType, e.RankNumber }, "route_recommendations_trip_search_id_recommendation_type_ra_key").IsUnique();

            entity.Property(e => e.RecommendationId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.RankNumber).HasDefaultValue(1);
            entity.Property(e => e.RecommendationScore).HasPrecision(12, 6);
            entity.Property(e => e.RecommendationType).HasMaxLength(30);
            entity.Property(e => e.TotalDistanceMeters).HasPrecision(12, 2);
            entity.Property(e => e.TotalFare).HasPrecision(10, 2);
            entity.Property(e => e.TotalMinutes).HasPrecision(10, 2);
            entity.Property(e => e.TransferCount).HasDefaultValue(0);
            entity.Property(e => e.WalkingDistanceMeters).HasPrecision(12, 2);

            entity.HasOne(d => d.TripSearch).WithMany(p => p.RouteRecommendations)
                .HasForeignKey(d => d.TripSearchId)
                .HasConstraintName("route_recommendations_trip_search_id_fkey");
        });

        modelBuilder.Entity<RouteSegment>(entity =>
        {
            entity.HasKey(e => e.SegmentId).HasName("route_segments_pkey");

            entity.HasIndex(e => e.FromStopId, "idx_route_segments_from_stop");

            entity.HasIndex(e => new { e.RouteId, e.SegmentOrder }, "idx_route_segments_route");

            entity.HasIndex(e => e.ToStopId, "idx_route_segments_to_stop");

            entity.HasIndex(e => new { e.RouteId, e.SegmentOrder }, "route_segments_route_id_segment_order_key").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DistanceMeters).HasPrecision(12, 2);
            entity.Property(e => e.EstimatedFare).HasPrecision(10, 2);
            entity.Property(e => e.EstimatedMinutes).HasPrecision(10, 2);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsBidirectional).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.FromStop).WithMany(p => p.SegmentsStartingHere)
                .HasForeignKey(d => d.FromStopId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("route_segments_from_stop_id_fkey");

            entity.HasOne(d => d.Route).WithMany(p => p.RouteSegments)
                .HasForeignKey(d => d.RouteId)
                .HasConstraintName("route_segments_route_id_fkey");

            entity.HasOne(d => d.ToStop).WithMany(p => p.SegmentsEndingHere)
                .HasForeignKey(d => d.ToStopId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("route_segments_to_stop_id_fkey");
        });

        modelBuilder.Entity<RouteStop>(entity =>
        {
            entity.HasKey(e => e.RouteStopId).HasName("route_stops_pkey");

            entity.HasIndex(e => new { e.RouteId, e.StopOrder }, "idx_route_stops_route");

            entity.HasIndex(e => e.StopId, "idx_route_stops_stop");

            entity.HasIndex(e => new { e.RouteId, e.StopId }, "route_stops_route_id_stop_id_key").IsUnique();

            entity.HasIndex(e => new { e.RouteId, e.StopOrder }, "route_stops_route_id_stop_order_key").IsUnique();

            entity.Property(e => e.RouteStopId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CanAlight).HasDefaultValue(true);
            entity.Property(e => e.CanBoard).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Route).WithMany(p => p.RouteStops)
                .HasForeignKey(d => d.RouteId)
                .HasConstraintName("route_stops_route_id_fkey");

            entity.HasOne(d => d.Stop).WithMany(p => p.RouteStops)
                .HasForeignKey(d => d.StopId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("route_stops_stop_id_fkey");
        });

        modelBuilder.Entity<TransportMode>(entity =>
        {
            entity.HasKey(e => e.TransportModeId).HasName("transport_modes_pkey");

            entity.HasIndex(e => e.Code, "transport_modes_code_key").IsUnique();

            entity.HasIndex(e => e.Name, "transport_modes_name_key").IsUnique();

            entity.Property(e => e.AllowsLiveDriver).HasDefaultValue(false);
            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IconName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsMotorized).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<TransportRoute>(entity =>
        {
            entity.HasKey(e => e.RouteId).HasName("transport_routes_pkey");

            entity.HasIndex(e => e.TransportModeId, "idx_transport_routes_mode");

            entity.HasIndex(e => e.RouteCode, "transport_routes_route_code_key").IsUnique();

            entity.Property(e => e.RouteId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.BaseFare).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OperatesFriday).HasDefaultValue(true);
            entity.Property(e => e.OperatesMonday).HasDefaultValue(true);
            entity.Property(e => e.OperatesSaturday).HasDefaultValue(true);
            entity.Property(e => e.OperatesSunday).HasDefaultValue(true);
            entity.Property(e => e.OperatesThursday).HasDefaultValue(true);
            entity.Property(e => e.OperatesTuesday).HasDefaultValue(true);
            entity.Property(e => e.OperatesWednesday).HasDefaultValue(true);
            entity.Property(e => e.RouteCode).HasMaxLength(50);
            entity.Property(e => e.RouteName).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.EndStop).WithMany(p => p.RoutesEndingHere)
                .HasForeignKey(d => d.EndStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("transport_routes_end_stop_id_fkey");

            entity.HasOne(d => d.StartStop).WithMany(p => p.RoutesStartingHere)
                .HasForeignKey(d => d.StartStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("transport_routes_start_stop_id_fkey");

            entity.HasOne(d => d.TransportMode).WithMany(p => p.TransportRoutes)
                .HasForeignKey(d => d.TransportModeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("transport_routes_transport_mode_id_fkey");
        });

        modelBuilder.Entity<TransportStop>(entity =>
        {
            entity.HasKey(e => e.StopId).HasName("transport_stops_pkey");

            entity.HasIndex(e => e.Name, "idx_transport_stops_name");

            entity.HasIndex(e => e.StopCode, "transport_stops_stop_code_key").IsUnique();

            entity.Property(e => e.StopId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.StopCode).HasMaxLength(50);
            entity.Property(e => e.StopType).HasMaxLength(30);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<TripAlert>(entity =>
        {
            entity.HasKey(e => e.AlertId).HasName("trip_alerts_pkey");

            entity.HasIndex(e => e.PassengerTripId, "idx_trip_alerts_trip");

            entity.Property(e => e.AlertId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AlertType).HasMaxLength(40);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsTriggered).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(150);
            entity.Property(e => e.TriggerDistanceMeters).HasPrecision(10, 2);

            entity.HasOne(d => d.Leg).WithMany(p => p.TripAlerts)
                .HasForeignKey(d => d.LegId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trip_alerts_leg_id_fkey");

            entity.HasOne(d => d.PassengerTrip).WithMany(p => p.TripAlerts)
                .HasForeignKey(d => d.PassengerTripId)
                .HasConstraintName("trip_alerts_passenger_trip_id_fkey");

            entity.HasOne(d => d.TargetStop).WithMany(p => p.TripAlerts)
                .HasForeignKey(d => d.TargetStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trip_alerts_target_stop_id_fkey");
        });

        modelBuilder.Entity<TripSearch>(entity =>
        {
            entity.HasKey(e => e.TripSearchId).HasName("trip_searches_pkey");

            entity.HasIndex(e => e.UserId, "idx_trip_searches_user");

            entity.Property(e => e.TripSearchId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Budget).HasPrecision(10, 2);
            entity.Property(e => e.DestinationName).HasMaxLength(250);
            entity.Property(e => e.OriginName).HasMaxLength(250);
            entity.Property(e => e.PassengerCount).HasDefaultValue(1);
            entity.Property(e => e.Preference).HasMaxLength(30);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithMany(p => p.TripSearches)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trip_searches_user_id_fkey");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("user_profiles_pkey");

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PASSENGER'::character varying");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        OnModelCreatingPartial(modelBuilder);
        ApplyDatabaseNaming(modelBuilder);
    }

    private static void ApplyDatabaseNaming(ModelBuilder modelBuilder)
    {
        var tableNames = new Dictionary<Type, string>
        {
            [typeof(ChatConversation)] = "chat_conversations",
            [typeof(ChatMessage)] = "chat_messages",
            [typeof(Driver)] = "drivers",
            [typeof(DriverAvailabilitySession)] = "driver_availability_sessions",
            [typeof(DriverLocation)] = "driver_locations",
            [typeof(DriverVehicle)] = "driver_vehicles",
            [typeof(FareRule)] = "fare_rules",
            [typeof(PassengerRideRequest)] = "passenger_ride_requests",
            [typeof(PassengerTrip)] = "passenger_trips",
            [typeof(RecommendationLeg)] = "recommendation_legs",
            [typeof(RideMatch)] = "ride_matches",
            [typeof(RouteRecommendation)] = "route_recommendations",
            [typeof(RouteSegment)] = "route_segments",
            [typeof(RouteStop)] = "route_stops",
            [typeof(TransportMode)] = "transport_modes",
            [typeof(TransportRoute)] = "transport_routes",
            [typeof(TransportStop)] = "transport_stops",
            [typeof(TripAlert)] = "trip_alerts",
            [typeof(TripSearch)] = "trip_searches",
            [typeof(UserProfile)] = "user_profiles",
        };

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (tableNames.TryGetValue(entityType.ClrType, out var tableName))
            {
                entityType.SetTableName(tableName);
            }

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsUpper(character))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
