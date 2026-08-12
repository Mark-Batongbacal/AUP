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

    public virtual DbSet<chat_conversation> chat_conversations { get; set; }

    public virtual DbSet<chat_message> chat_messages { get; set; }

    public virtual DbSet<driver> drivers { get; set; }

    public virtual DbSet<driver_availability_session> driver_availability_sessions { get; set; }

    public virtual DbSet<driver_location> driver_locations { get; set; }

    public virtual DbSet<driver_vehicle> driver_vehicles { get; set; }

    public virtual DbSet<fare_rule> fare_rules { get; set; }

    public virtual DbSet<passenger_ride_request> passenger_ride_requests { get; set; }

    public virtual DbSet<passenger_trip> passenger_trips { get; set; }

    public virtual DbSet<recommendation_leg> recommendation_legs { get; set; }

    public virtual DbSet<ride_match> ride_matches { get; set; }

    public virtual DbSet<route_recommendation> route_recommendations { get; set; }

    public virtual DbSet<route_segment> route_segments { get; set; }

    public virtual DbSet<route_stop> route_stops { get; set; }

    public virtual DbSet<transport_mode> transport_modes { get; set; }

    public virtual DbSet<transport_route> transport_routes { get; set; }

    public virtual DbSet<transport_stop> transport_stops { get; set; }

    public virtual DbSet<trip_alert> trip_alerts { get; set; }

    public virtual DbSet<trip_search> trip_searches { get; set; }

    public virtual DbSet<user_profile> user_profiles { get; set; }

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

        modelBuilder.Entity<chat_conversation>(entity =>
        {
            entity.HasKey(e => e.conversation_id).HasName("chat_conversations_pkey");

            entity.HasIndex(e => e.user_id, "idx_chat_conversations_user");

            entity.Property(e => e.conversation_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.title).HasMaxLength(200);
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.user).WithMany(p => p.chat_conversations)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("chat_conversations_user_id_fkey");
        });

        modelBuilder.Entity<chat_message>(entity =>
        {
            entity.HasKey(e => e.message_id).HasName("chat_messages_pkey");

            entity.HasIndex(e => new { e.conversation_id, e.created_at }, "idx_chat_messages_conversation");

            entity.Property(e => e.message_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.extracted_budget).HasPrecision(10, 2);
            entity.Property(e => e.sender).HasMaxLength(20);

            entity.HasOne(d => d.conversation).WithMany(p => p.chat_messages)
                .HasForeignKey(d => d.conversation_id)
                .HasConstraintName("chat_messages_conversation_id_fkey");

            entity.HasOne(d => d.trip_search).WithMany(p => p.chat_messages)
                .HasForeignKey(d => d.trip_search_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("chat_messages_trip_search_id_fkey");
        });

        modelBuilder.Entity<driver>(entity =>
        {
            entity.HasKey(e => e.driver_id).HasName("drivers_pkey");

            entity.HasIndex(e => e.user_id, "drivers_user_id_key").IsUnique();

            entity.Property(e => e.driver_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.average_rating).HasPrecision(3, 2);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.is_available).HasDefaultValue(false);
            entity.Property(e => e.license_number).HasMaxLength(100);
            entity.Property(e => e.rating_count).HasDefaultValue(0);
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");
            entity.Property(e => e.verification_status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'PENDING'::character varying");

            entity.HasOne(d => d.home_terminal).WithMany(p => p.drivers)
                .HasForeignKey(d => d.home_terminal_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("drivers_home_terminal_id_fkey");

            entity.HasOne(d => d.user).WithOne(p => p.driver)
                .HasForeignKey<driver>(d => d.user_id)
                .HasConstraintName("drivers_user_id_fkey");
        });

        modelBuilder.Entity<driver_availability_session>(entity =>
        {
            entity.HasKey(e => e.session_id).HasName("driver_availability_sessions_pkey");

            entity.HasIndex(e => e.driver_id, "idx_driver_sessions_driver");

            entity.Property(e => e.session_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.available_seats).HasDefaultValue(1);
            entity.Property(e => e.destination_name).HasMaxLength(250);
            entity.Property(e => e.maximum_detour_meters)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("1000");
            entity.Property(e => e.started_at).HasDefaultValueSql("now()");
            entity.Property(e => e.status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'AVAILABLE'::character varying");

            entity.HasOne(d => d.destination_stop).WithMany(p => p.driver_availability_sessions)
                .HasForeignKey(d => d.destination_stop_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("driver_availability_sessions_destination_stop_id_fkey");

            entity.HasOne(d => d.driver).WithMany(p => p.driver_availability_sessions)
                .HasForeignKey(d => d.driver_id)
                .HasConstraintName("driver_availability_sessions_driver_id_fkey");

            entity.HasOne(d => d.vehicle).WithMany(p => p.driver_availability_sessions)
                .HasForeignKey(d => d.vehicle_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("driver_availability_sessions_vehicle_id_fkey");
        });

        modelBuilder.Entity<driver_location>(entity =>
        {
            entity.HasKey(e => e.driver_id).HasName("driver_locations_pkey");

            entity.Property(e => e.driver_id).ValueGeneratedNever();
            entity.Property(e => e.accuracy_meters).HasPrecision(8, 2);
            entity.Property(e => e.heading_degrees).HasPrecision(6, 2);
            entity.Property(e => e.speed_kph).HasPrecision(8, 2);
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.driver).WithOne(p => p.driver_location)
                .HasForeignKey<driver_location>(d => d.driver_id)
                .HasConstraintName("driver_locations_driver_id_fkey");
        });

        modelBuilder.Entity<driver_vehicle>(entity =>
        {
            entity.HasKey(e => e.vehicle_id).HasName("driver_vehicles_pkey");

            entity.HasIndex(e => new { e.driver_id, e.plate_number }, "driver_vehicles_driver_id_plate_number_key").IsUnique();

            entity.Property(e => e.vehicle_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.body_number).HasMaxLength(50);
            entity.Property(e => e.capacity).HasDefaultValue(1);
            entity.Property(e => e.color).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.plate_number).HasMaxLength(50);

            entity.HasOne(d => d.driver).WithMany(p => p.driver_vehicles)
                .HasForeignKey(d => d.driver_id)
                .HasConstraintName("driver_vehicles_driver_id_fkey");

            entity.HasOne(d => d.transport_mode).WithMany(p => p.driver_vehicles)
                .HasForeignKey(d => d.transport_mode_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("driver_vehicles_transport_mode_id_fkey");
        });

        modelBuilder.Entity<fare_rule>(entity =>
        {
            entity.HasKey(e => e.fare_rule_id).HasName("fare_rules_pkey");

            entity.HasIndex(e => e.route_id, "idx_fare_rules_route");

            entity.Property(e => e.fare_rule_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.additional_fare_per_km).HasPrecision(10, 2);
            entity.Property(e => e.base_distance_km).HasPrecision(10, 2);
            entity.Property(e => e.base_fare).HasPrecision(10, 2);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.effective_from).HasDefaultValueSql("CURRENT_DATE");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.maximum_fare).HasPrecision(10, 2);
            entity.Property(e => e.minimum_fare).HasPrecision(10, 2);
            entity.Property(e => e.rule_name).HasMaxLength(150);

            entity.HasOne(d => d.route).WithMany(p => p.fare_rules)
                .HasForeignKey(d => d.route_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fare_rules_route_id_fkey");

            entity.HasOne(d => d.transport_mode).WithMany(p => p.fare_rules)
                .HasForeignKey(d => d.transport_mode_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fare_rules_transport_mode_id_fkey");
        });

        modelBuilder.Entity<passenger_ride_request>(entity =>
        {
            entity.HasKey(e => e.request_id).HasName("passenger_ride_requests_pkey");

            entity.HasIndex(e => e.passenger_user_id, "idx_passenger_requests_passenger");

            entity.HasIndex(e => e.status, "idx_passenger_requests_status");

            entity.Property(e => e.request_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.dropoff_name).HasMaxLength(250);
            entity.Property(e => e.max_budget).HasPrecision(10, 2);
            entity.Property(e => e.passenger_count).HasDefaultValue(1);
            entity.Property(e => e.pickup_name).HasMaxLength(250);
            entity.Property(e => e.requested_at).HasDefaultValueSql("now()");
            entity.Property(e => e.status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'SEARCHING'::character varying");
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.passenger_user).WithMany(p => p.passenger_ride_requests)
                .HasForeignKey(d => d.passenger_user_id)
                .HasConstraintName("passenger_ride_requests_passenger_user_id_fkey");

            entity.HasOne(d => d.transport_mode).WithMany(p => p.passenger_ride_requests)
                .HasForeignKey(d => d.transport_mode_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("passenger_ride_requests_transport_mode_id_fkey");
        });

        modelBuilder.Entity<passenger_trip>(entity =>
        {
            entity.HasKey(e => e.passenger_trip_id).HasName("passenger_trips_pkey");

            entity.HasIndex(e => e.user_id, "idx_passenger_trips_user");

            entity.Property(e => e.passenger_trip_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.current_leg_order).HasDefaultValue(1);
            entity.Property(e => e.status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'PLANNED'::character varying");
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.recommendation).WithMany(p => p.passenger_trips)
                .HasForeignKey(d => d.recommendation_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("passenger_trips_recommendation_id_fkey");

            entity.HasOne(d => d.user).WithMany(p => p.passenger_trips)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("passenger_trips_user_id_fkey");
        });

        modelBuilder.Entity<recommendation_leg>(entity =>
        {
            entity.HasKey(e => e.leg_id).HasName("recommendation_legs_pkey");

            entity.HasIndex(e => new { e.recommendation_id, e.leg_order }, "idx_recommendation_legs_recommendation");

            entity.HasIndex(e => new { e.recommendation_id, e.leg_order }, "recommendation_legs_recommendation_id_leg_order_key").IsUnique();

            entity.Property(e => e.leg_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.distance_meters).HasPrecision(12, 2);
            entity.Property(e => e.estimated_fare).HasPrecision(10, 2);
            entity.Property(e => e.estimated_minutes).HasPrecision(10, 2);
            entity.Property(e => e.from_name).HasMaxLength(250);
            entity.Property(e => e.to_name).HasMaxLength(250);

            entity.HasOne(d => d.from_stop).WithMany(p => p.recommendation_legfrom_stops)
                .HasForeignKey(d => d.from_stop_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("recommendation_legs_from_stop_id_fkey");

            entity.HasOne(d => d.recommendation).WithMany(p => p.recommendation_legs)
                .HasForeignKey(d => d.recommendation_id)
                .HasConstraintName("recommendation_legs_recommendation_id_fkey");

            entity.HasOne(d => d.route).WithMany(p => p.recommendation_legs)
                .HasForeignKey(d => d.route_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("recommendation_legs_route_id_fkey");

            entity.HasOne(d => d.to_stop).WithMany(p => p.recommendation_legto_stops)
                .HasForeignKey(d => d.to_stop_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("recommendation_legs_to_stop_id_fkey");

            entity.HasOne(d => d.transport_mode).WithMany(p => p.recommendation_legs)
                .HasForeignKey(d => d.transport_mode_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("recommendation_legs_transport_mode_id_fkey");
        });

        modelBuilder.Entity<ride_match>(entity =>
        {
            entity.HasKey(e => e.match_id).HasName("ride_matches_pkey");

            entity.HasIndex(e => e.driver_id, "idx_ride_matches_driver");

            entity.HasIndex(e => e.request_id, "idx_ride_matches_request");

            entity.HasIndex(e => new { e.request_id, e.driver_id, e.session_id }, "ride_matches_request_id_driver_id_session_id_key").IsUnique();

            entity.Property(e => e.match_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.detour_distance_meters).HasPrecision(10, 2);
            entity.Property(e => e.estimated_fare).HasPrecision(10, 2);
            entity.Property(e => e.estimated_pickup_minutes).HasPrecision(10, 2);
            entity.Property(e => e.estimated_trip_minutes).HasPrecision(10, 2);
            entity.Property(e => e.match_score).HasPrecision(12, 6);
            entity.Property(e => e.offered_at).HasDefaultValueSql("now()");
            entity.Property(e => e.pickup_distance_meters).HasPrecision(10, 2);
            entity.Property(e => e.status)
                .HasMaxLength(30)
                .HasDefaultValueSql("'OFFERED'::character varying");

            entity.HasOne(d => d.driver).WithMany(p => p.ride_matches)
                .HasForeignKey(d => d.driver_id)
                .HasConstraintName("ride_matches_driver_id_fkey");

            entity.HasOne(d => d.request).WithMany(p => p.ride_matches)
                .HasForeignKey(d => d.request_id)
                .HasConstraintName("ride_matches_request_id_fkey");

            entity.HasOne(d => d.session).WithMany(p => p.ride_matches)
                .HasForeignKey(d => d.session_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("ride_matches_session_id_fkey");

            entity.HasOne(d => d.vehicle).WithMany(p => p.ride_matches)
                .HasForeignKey(d => d.vehicle_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("ride_matches_vehicle_id_fkey");
        });

        modelBuilder.Entity<route_recommendation>(entity =>
        {
            entity.HasKey(e => e.recommendation_id).HasName("route_recommendations_pkey");

            entity.HasIndex(e => e.trip_search_id, "idx_route_recommendations_search");

            entity.HasIndex(e => new { e.trip_search_id, e.recommendation_type, e.rank_number }, "route_recommendations_trip_search_id_recommendation_type_ra_key").IsUnique();

            entity.Property(e => e.recommendation_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.generated_at).HasDefaultValueSql("now()");
            entity.Property(e => e.rank_number).HasDefaultValue(1);
            entity.Property(e => e.recommendation_score).HasPrecision(12, 6);
            entity.Property(e => e.recommendation_type).HasMaxLength(30);
            entity.Property(e => e.total_distance_meters).HasPrecision(12, 2);
            entity.Property(e => e.total_fare).HasPrecision(10, 2);
            entity.Property(e => e.total_minutes).HasPrecision(10, 2);
            entity.Property(e => e.transfer_count).HasDefaultValue(0);
            entity.Property(e => e.walking_distance_meters).HasPrecision(12, 2);

            entity.HasOne(d => d.trip_search).WithMany(p => p.route_recommendations)
                .HasForeignKey(d => d.trip_search_id)
                .HasConstraintName("route_recommendations_trip_search_id_fkey");
        });

        modelBuilder.Entity<route_segment>(entity =>
        {
            entity.HasKey(e => e.segment_id).HasName("route_segments_pkey");

            entity.HasIndex(e => e.from_stop_id, "idx_route_segments_from_stop");

            entity.HasIndex(e => new { e.route_id, e.segment_order }, "idx_route_segments_route");

            entity.HasIndex(e => e.to_stop_id, "idx_route_segments_to_stop");

            entity.HasIndex(e => new { e.route_id, e.segment_order }, "route_segments_route_id_segment_order_key").IsUnique();

            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.distance_meters).HasPrecision(12, 2);
            entity.Property(e => e.estimated_fare).HasPrecision(10, 2);
            entity.Property(e => e.estimated_minutes).HasPrecision(10, 2);
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.is_bidirectional).HasDefaultValue(false);
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.from_stop).WithMany(p => p.route_segmentfrom_stops)
                .HasForeignKey(d => d.from_stop_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("route_segments_from_stop_id_fkey");

            entity.HasOne(d => d.route).WithMany(p => p.route_segments)
                .HasForeignKey(d => d.route_id)
                .HasConstraintName("route_segments_route_id_fkey");

            entity.HasOne(d => d.to_stop).WithMany(p => p.route_segmentto_stops)
                .HasForeignKey(d => d.to_stop_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("route_segments_to_stop_id_fkey");
        });

        modelBuilder.Entity<route_stop>(entity =>
        {
            entity.HasKey(e => e.route_stop_id).HasName("route_stops_pkey");

            entity.HasIndex(e => new { e.route_id, e.stop_order }, "idx_route_stops_route");

            entity.HasIndex(e => e.stop_id, "idx_route_stops_stop");

            entity.HasIndex(e => new { e.route_id, e.stop_id }, "route_stops_route_id_stop_id_key").IsUnique();

            entity.HasIndex(e => new { e.route_id, e.stop_order }, "route_stops_route_id_stop_order_key").IsUnique();

            entity.Property(e => e.route_stop_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.can_alight).HasDefaultValue(true);
            entity.Property(e => e.can_board).HasDefaultValue(true);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.route).WithMany(p => p.route_stops)
                .HasForeignKey(d => d.route_id)
                .HasConstraintName("route_stops_route_id_fkey");

            entity.HasOne(d => d.stop).WithMany(p => p.route_stops)
                .HasForeignKey(d => d.stop_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("route_stops_stop_id_fkey");
        });

        modelBuilder.Entity<transport_mode>(entity =>
        {
            entity.HasKey(e => e.transport_mode_id).HasName("transport_modes_pkey");

            entity.HasIndex(e => e.code, "transport_modes_code_key").IsUnique();

            entity.HasIndex(e => e.name, "transport_modes_name_key").IsUnique();

            entity.Property(e => e.allows_live_driver).HasDefaultValue(false);
            entity.Property(e => e.code).HasMaxLength(30);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.icon_name).HasMaxLength(100);
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.is_motorized).HasDefaultValue(true);
            entity.Property(e => e.name).HasMaxLength(50);
        });

        modelBuilder.Entity<transport_route>(entity =>
        {
            entity.HasKey(e => e.route_id).HasName("transport_routes_pkey");

            entity.HasIndex(e => e.transport_mode_id, "idx_transport_routes_mode");

            entity.HasIndex(e => e.route_code, "transport_routes_route_code_key").IsUnique();

            entity.Property(e => e.route_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.base_fare).HasPrecision(10, 2);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.operates_friday).HasDefaultValue(true);
            entity.Property(e => e.operates_monday).HasDefaultValue(true);
            entity.Property(e => e.operates_saturday).HasDefaultValue(true);
            entity.Property(e => e.operates_sunday).HasDefaultValue(true);
            entity.Property(e => e.operates_thursday).HasDefaultValue(true);
            entity.Property(e => e.operates_tuesday).HasDefaultValue(true);
            entity.Property(e => e.operates_wednesday).HasDefaultValue(true);
            entity.Property(e => e.route_code).HasMaxLength(50);
            entity.Property(e => e.route_name).HasMaxLength(200);
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.end_stop).WithMany(p => p.transport_routeend_stops)
                .HasForeignKey(d => d.end_stop_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("transport_routes_end_stop_id_fkey");

            entity.HasOne(d => d.start_stop).WithMany(p => p.transport_routestart_stops)
                .HasForeignKey(d => d.start_stop_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("transport_routes_start_stop_id_fkey");

            entity.HasOne(d => d.transport_mode).WithMany(p => p.transport_routes)
                .HasForeignKey(d => d.transport_mode_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("transport_routes_transport_mode_id_fkey");
        });

        modelBuilder.Entity<transport_stop>(entity =>
        {
            entity.HasKey(e => e.stop_id).HasName("transport_stops_pkey");

            entity.HasIndex(e => e.name, "idx_transport_stops_name");

            entity.HasIndex(e => e.stop_code, "transport_stops_stop_code_key").IsUnique();

            entity.Property(e => e.stop_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.name).HasMaxLength(200);
            entity.Property(e => e.stop_code).HasMaxLength(50);
            entity.Property(e => e.stop_type).HasMaxLength(30);
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<trip_alert>(entity =>
        {
            entity.HasKey(e => e.alert_id).HasName("trip_alerts_pkey");

            entity.HasIndex(e => e.passenger_trip_id, "idx_trip_alerts_trip");

            entity.Property(e => e.alert_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.alert_type).HasMaxLength(40);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.is_triggered).HasDefaultValue(false);
            entity.Property(e => e.title).HasMaxLength(150);
            entity.Property(e => e.trigger_distance_meters).HasPrecision(10, 2);

            entity.HasOne(d => d.leg).WithMany(p => p.trip_alerts)
                .HasForeignKey(d => d.leg_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trip_alerts_leg_id_fkey");

            entity.HasOne(d => d.passenger_trip).WithMany(p => p.trip_alerts)
                .HasForeignKey(d => d.passenger_trip_id)
                .HasConstraintName("trip_alerts_passenger_trip_id_fkey");

            entity.HasOne(d => d.target_stop).WithMany(p => p.trip_alerts)
                .HasForeignKey(d => d.target_stop_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trip_alerts_target_stop_id_fkey");
        });

        modelBuilder.Entity<trip_search>(entity =>
        {
            entity.HasKey(e => e.trip_search_id).HasName("trip_searches_pkey");

            entity.HasIndex(e => e.user_id, "idx_trip_searches_user");

            entity.Property(e => e.trip_search_id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.budget).HasPrecision(10, 2);
            entity.Property(e => e.destination_name).HasMaxLength(250);
            entity.Property(e => e.origin_name).HasMaxLength(250);
            entity.Property(e => e.passenger_count).HasDefaultValue(1);
            entity.Property(e => e.preference).HasMaxLength(30);
            entity.Property(e => e.requested_at).HasDefaultValueSql("now()");

            entity.HasOne(d => d.user).WithMany(p => p.trip_searches)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("trip_searches_user_id_fkey");
        });

        modelBuilder.Entity<user_profile>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("user_profiles_pkey");

            entity.Property(e => e.user_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.first_name).HasMaxLength(100);
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.last_name).HasMaxLength(100);
            entity.Property(e => e.phone_number).HasMaxLength(30);
            entity.Property(e => e.role)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PASSENGER'::character varying");
            entity.Property(e => e.updated_at).HasDefaultValueSql("now()");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
