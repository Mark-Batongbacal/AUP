using Microsoft.EntityFrameworkCore;

namespace backend.Models.Database;

public partial class TukiDbContext : DbContext
{
    public TukiDbContext(DbContextOptions<TukiDbContext> options)
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

    public virtual DbSet<RoutePoint> RoutePoints { get; set; }

    public virtual DbSet<RouteRecommendation> RouteRecommendations { get; set; }

    public virtual DbSet<RouteSegment> RouteSegments { get; set; }

    public virtual DbSet<RouteStop> RouteStops { get; set; }

    public virtual DbSet<TransferConnection> TransferConnections { get; set; }

    public virtual DbSet<TransportMode> TransportModes { get; set; }

    public virtual DbSet<TransportRoute> TransportRoutes { get; set; }

    public virtual DbSet<TransportStop> TransportStops { get; set; }

    public virtual DbSet<TricyclePoint> TricyclePoints { get; set; }

    public virtual DbSet<TripAlert> TripAlerts { get; set; }

    public virtual DbSet<TripSearch> TripSearches { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        ConfigureUserProfiles(modelBuilder);
        ConfigureTransportModes(modelBuilder);
        ConfigureTransportStops(modelBuilder);
        ConfigureTransportRoutes(modelBuilder);
        ConfigureRoutePoints(modelBuilder);
        ConfigureRouteStops(modelBuilder);
        ConfigureRouteSegments(modelBuilder);
        ConfigureFareRules(modelBuilder);
        ConfigureTricyclePoints(modelBuilder);
        ConfigureTransferConnections(modelBuilder);
        ConfigureDrivers(modelBuilder);
        ConfigureDriverVehicles(modelBuilder);
        ConfigureDriverLocations(modelBuilder);
        ConfigureDriverAvailabilitySessions(modelBuilder);
        ConfigurePassengerRideRequests(modelBuilder);
        ConfigureRideMatches(modelBuilder);
        ConfigureTripSearches(modelBuilder);
        ConfigureRouteRecommendations(modelBuilder);
        ConfigureRecommendationLegs(modelBuilder);
        ConfigurePassengerTrips(modelBuilder);
        ConfigureTripAlerts(modelBuilder);
        ConfigureChatConversations(modelBuilder);
        ConfigureChatMessages(modelBuilder);

        OnModelCreatingPartial(modelBuilder);
    }

    private static void ConfigureUserProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("UserProfiles", "dbo");
            entity.HasKey(e => e.UserId);

            entity.HasIndex(e => e.Email, "UX_UserProfiles_Email").IsUnique();
            entity.HasIndex(e => new { e.ExternalAuthProvider, e.ExternalAuthId }, "UX_UserProfiles_ExternalAuthentication")
                .IsUnique()
                .HasFilter("([ExternalAuthProvider] IS NOT NULL AND [ExternalAuthId] IS NOT NULL)");

            entity.Property(e => e.UserId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.ExternalAuthProvider).HasMaxLength(50);
            entity.Property(e => e.ExternalAuthId).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.Role).HasMaxLength(30).HasDefaultValue("Passenger");
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });
    }

    private static void ConfigureTransportModes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransportMode>(entity =>
        {
            entity.ToTable("TransportModes", "dbo");
            entity.HasKey(e => e.TransportModeId);

            entity.HasIndex(e => e.Code, "UX_TransportModes_ModeCode").IsUnique();
            entity.HasIndex(e => e.Name, "UX_TransportModes_ModeName").IsUnique();

            entity.Property(e => e.Code).HasColumnName("ModeCode").HasMaxLength(30);
            entity.Property(e => e.Name).HasColumnName("ModeName").HasMaxLength(100);
            entity.Property(e => e.IsMotorized).HasDefaultValue(true);
            entity.Property(e => e.AllowsLiveDriver).HasDefaultValue(false);
            entity.Property(e => e.IconName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });
    }

    private static void ConfigureTransportStops(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransportStop>(entity =>
        {
            entity.ToTable("TransportStops", "dbo");
            entity.HasKey(e => e.StopId);

            entity.HasIndex(e => new { e.Latitude, e.Longitude }, "IX_TransportStops_Coordinates");
            entity.HasIndex(e => e.Name, "IX_TransportStops_StopName");
            entity.HasIndex(e => e.StopCode, "UX_TransportStops_StopCode")
                .IsUnique()
                .HasFilter("([StopCode] IS NOT NULL)");

            entity.Property(e => e.StopId).HasColumnName("TransportStopId");
            entity.Property(e => e.StopCode).HasMaxLength(50);
            entity.Property(e => e.Name).HasColumnName("StopName").HasMaxLength(200);
            entity.Property(e => e.StopType).HasMaxLength(30).HasDefaultValue("JeepneyStop");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.Ignore(e => e.SegmentsStartingHere);
            entity.Ignore(e => e.SegmentsEndingHere);
        });
    }

    private static void ConfigureTransportRoutes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransportRoute>(entity =>
        {
            entity.ToTable("TransportRoutes", "dbo");
            entity.HasKey(e => e.RouteId);

            entity.HasIndex(e => new { e.TransportModeId, e.IsActive }, "IX_TransportRoutes_TransportMode");
            entity.HasIndex(e => e.RouteCode, "UX_TransportRoutes_RouteCode").IsUnique();

            entity.Property(e => e.RouteId).HasColumnName("TransportRouteId");
            entity.Property(e => e.RouteCode).HasMaxLength(50);
            entity.Property(e => e.RouteName).HasMaxLength(200);
            entity.Property(e => e.StartStopId).HasColumnName("StartTransportStopId");
            entity.Property(e => e.EndStopId).HasColumnName("EndTransportStopId");
            entity.Property(e => e.OriginName).HasMaxLength(200);
            entity.Property(e => e.DestinationName).HasMaxLength(200);
            entity.Property(e => e.DirectionName).HasMaxLength(100);
            entity.Property(e => e.OperatorName).HasMaxLength(200);
            entity.Property(e => e.RouteDescription).HasColumnName("Description").HasMaxLength(1000);
            entity.Property(e => e.BaseFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.OperatesMonday).HasDefaultValue(true);
            entity.Property(e => e.OperatesTuesday).HasDefaultValue(true);
            entity.Property(e => e.OperatesWednesday).HasDefaultValue(true);
            entity.Property(e => e.OperatesThursday).HasDefaultValue(true);
            entity.Property(e => e.OperatesFriday).HasDefaultValue(true);
            entity.Property(e => e.OperatesSaturday).HasDefaultValue(true);
            entity.Property(e => e.OperatesSunday).HasDefaultValue(true);

            entity.HasOne(e => e.TransportMode)
                .WithMany(e => e.TransportRoutes)
                .HasForeignKey(e => e.TransportModeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TransportRoutes_TransportModes");

            entity.HasOne(e => e.StartStop)
                .WithMany(e => e.RoutesStartingHere)
                .HasForeignKey(e => e.StartStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TransportRoutes_StartStop");

            entity.HasOne(e => e.EndStop)
                .WithMany(e => e.RoutesEndingHere)
                .HasForeignKey(e => e.EndStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TransportRoutes_EndStop");
        });
    }

    private static void ConfigureRoutePoints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoutePoint>(entity =>
        {
            entity.ToTable("RoutePoints", "dbo");
            entity.HasKey(e => e.RoutePointId);

            entity.HasIndex(e => new { e.RouteId, e.PointOrder }, "IX_RoutePoints_Route");
            entity.HasIndex(e => new { e.RouteId, e.PointOrder }, "UQ_RoutePoints_RouteAndOrder").IsUnique();

            entity.Property(e => e.RouteId).HasColumnName("TransportRouteId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Route)
                .WithMany(e => e.RoutePoints)
                .HasForeignKey(e => e.RouteId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RoutePoints_TransportRoutes");
        });
    }

    private static void ConfigureRouteStops(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteStop>(entity =>
        {
            entity.ToTable("RouteStops", "dbo");
            entity.HasKey(e => e.RouteStopId);

            entity.HasIndex(e => new { e.StopId, e.RouteId }, "IX_RouteStops_Stop");
            entity.HasIndex(e => new { e.RouteId, e.StopOrder }, "UQ_RouteStops_RouteAndOrder").IsUnique();
            entity.HasIndex(e => new { e.RouteId, e.StopId }, "UQ_RouteStops_RouteAndStop").IsUnique();

            entity.Property(e => e.RouteId).HasColumnName("TransportRouteId");
            entity.Property(e => e.StopId).HasColumnName("TransportStopId");
            entity.Property(e => e.EstimatedMinutesFromStart).HasColumnName("EstimatedTimeFromStartSeconds");
            entity.Property(e => e.Instructions).HasMaxLength(500);
            entity.Property(e => e.CanBoard).HasDefaultValue(true);
            entity.Property(e => e.CanAlight).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Route)
                .WithMany(e => e.RouteStops)
                .HasForeignKey(e => e.RouteId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RouteStops_TransportRoutes");

            entity.HasOne(e => e.Stop)
                .WithMany(e => e.RouteStops)
                .HasForeignKey(e => e.StopId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RouteStops_TransportStops");
        });
    }

    private static void ConfigureRouteSegments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteSegment>(entity =>
        {
            entity.ToTable("RouteSegments", "dbo");
            entity.HasKey(e => e.SegmentId);

            entity.HasIndex(e => new { e.RouteId, e.SegmentOrder }, "UQ_RouteSegments_RouteAndOrder").IsUnique();
            entity.HasIndex(e => e.FromRouteStopId, "IX_RouteSegments_FromRouteStop");
            entity.HasIndex(e => e.ToRouteStopId, "IX_RouteSegments_ToRouteStop");

            entity.Property(e => e.SegmentId).HasColumnName("RouteSegmentId");
            entity.Property(e => e.RouteId).HasColumnName("TransportRouteId");
            entity.Property(e => e.SegmentFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.IsBidirectional).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.Ignore(e => e.FromStopId);
            entity.Ignore(e => e.ToStopId);
            entity.Ignore(e => e.FromStop);
            entity.Ignore(e => e.ToStop);

            entity.HasOne(e => e.Route)
                .WithMany(e => e.RouteSegments)
                .HasForeignKey(e => e.RouteId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RouteSegments_TransportRoutes");

            entity.HasOne(e => e.FromRouteStop)
                .WithMany(e => e.RouteSegmentFromRouteStops)
                .HasForeignKey(e => e.FromRouteStopId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RouteSegments_FromRouteStop");

            entity.HasOne(e => e.ToRouteStop)
                .WithMany(e => e.RouteSegmentToRouteStops)
                .HasForeignKey(e => e.ToRouteStopId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RouteSegments_ToRouteStop");
        });
    }

    private static void ConfigureFareRules(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FareRule>(entity =>
        {
            entity.ToTable("FareRules", "dbo");
            entity.HasKey(e => e.FareRuleId);

            entity.HasIndex(e => new { e.RouteId, e.PassengerType, e.EffectiveFrom, e.EffectiveTo }, "IX_FareRules_RouteAndEffectiveDate");
            entity.HasIndex(e => e.TransportModeId, "IX_FareRules_TransportMode");

            entity.Property(e => e.RouteId).HasColumnName("TransportRouteId");
            entity.Property(e => e.PassengerType).HasMaxLength(30).HasDefaultValue("Regular");
            entity.Property(e => e.FareType).HasMaxLength(30).HasDefaultValue("Base");
            entity.Property(e => e.RuleName).HasMaxLength(150);
            entity.Property(e => e.BaseFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.BaseDistanceKm).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AdditionalFarePerKm).HasColumnName("AdditionalFarePerKilometer").HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MinimumFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MaximumFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.EffectiveFrom).HasDefaultValueSql("(CONVERT(date, sysutcdatetime()))");
            entity.Property(e => e.EffectiveTo).HasColumnName("EffectiveUntil");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Route)
                .WithMany(e => e.FareRules)
                .HasForeignKey(e => e.RouteId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_FareRules_TransportRoutes");

            entity.HasOne(e => e.TransportMode)
                .WithMany(e => e.FareRules)
                .HasForeignKey(e => e.TransportModeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FareRules_TransportModes");
        });
    }

    private static void ConfigureTricyclePoints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TricyclePoint>(entity =>
        {
            entity.ToTable("TricyclePoints", "dbo");
            entity.HasKey(e => e.TricyclePointId);

            entity.HasIndex(e => new { e.CenterLatitude, e.CenterLongitude }, "IX_TricyclePoints_Coordinates");
            entity.HasIndex(e => e.PointCode, "UX_TricyclePoints_PointCode").IsUnique();
            entity.HasIndex(e => e.StopId, "UX_TricyclePoints_TransportStop")
                .IsUnique()
                .HasFilter("([TransportStopId] IS NOT NULL)");

            entity.Property(e => e.StopId).HasColumnName("TransportStopId");
            entity.Property(e => e.PointCode).HasMaxLength(50);
            entity.Property(e => e.PointName).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.OperatorName).HasMaxLength(200);
            entity.Property(e => e.BaseFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.FarePerKilometer).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Stop)
                .WithOne(e => e.TricyclePoint)
                .HasForeignKey<TricyclePoint>(e => e.StopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TricyclePoints_TransportStops");
        });
    }

    private static void ConfigureTransferConnections(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransferConnection>(entity =>
        {
            entity.ToTable("TransferConnections", "dbo");
            entity.HasKey(e => e.TransferConnectionId);

            entity.HasIndex(e => new { e.FromStopId, e.ToStopId }, "UQ_TransferConnections_FromAndTo").IsUnique();

            entity.Property(e => e.FromStopId).HasColumnName("FromTransportStopId");
            entity.Property(e => e.ToStopId).HasColumnName("ToTransportStopId");
            entity.Property(e => e.Instructions).HasMaxLength(500);
            entity.Property(e => e.IsBidirectional).HasDefaultValue(true);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(e => e.FromStop)
                .WithMany(e => e.TransferConnectionsFromStop)
                .HasForeignKey(e => e.FromStopId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TransferConnections_FromStop");

            entity.HasOne(e => e.ToStop)
                .WithMany(e => e.TransferConnectionsToStop)
                .HasForeignKey(e => e.ToStopId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TransferConnections_ToStop");
        });
    }

    private static void ConfigureDrivers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Driver>(entity =>
        {
            entity.ToTable("Drivers", "dbo");
            entity.HasKey(e => e.DriverId);

            entity.HasIndex(e => e.UserId, "UQ_Drivers_UserId").IsUnique();
            entity.HasIndex(e => e.LicenseNumber, "UX_Drivers_LicenseNumber")
                .IsUnique()
                .HasFilter("([LicenseNumber] IS NOT NULL)");

            entity.Property(e => e.DriverId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.LicenseNumber).HasMaxLength(100);
            entity.Property(e => e.VerificationStatus).HasMaxLength(30).HasDefaultValue("PENDING");
            entity.Property(e => e.AverageRating).HasColumnType("decimal(3, 2)");
            entity.Property(e => e.RatingCount).HasDefaultValue(0);
            entity.Property(e => e.IsAvailable).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.Ignore(e => e.DriverLocation);

            entity.HasOne(e => e.User)
                .WithOne(e => e.Driver)
                .HasForeignKey<Driver>(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Drivers_UserProfiles");

            entity.HasOne(e => e.HomeTerminal)
                .WithMany(e => e.Drivers)
                .HasForeignKey(e => e.HomeTerminalId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Drivers_HomeTerminal");
        });
    }

    private static void ConfigureDriverVehicles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DriverVehicle>(entity =>
        {
            entity.ToTable("DriverVehicles", "dbo");
            entity.HasKey(e => e.VehicleId);

            entity.HasIndex(e => e.PlateNumber, "UX_DriverVehicles_PlateNumber")
                .IsUnique()
                .HasFilter("([PlateNumber] IS NOT NULL)");
            entity.HasIndex(e => new { e.DriverId, e.PlateNumber }, "UX_DriverVehicles_DriverAndPlate")
                .IsUnique()
                .HasFilter("([PlateNumber] IS NOT NULL)");
            entity.HasIndex(e => e.TransportModeId, "IX_DriverVehicles_TransportMode");

            entity.Property(e => e.VehicleId).HasColumnName("DriverVehicleId").HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.VehicleType).HasMaxLength(30).HasDefaultValue("Tricycle");
            entity.Property(e => e.PlateNumber).HasMaxLength(50);
            entity.Property(e => e.BodyNumber).HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Capacity).HasColumnName("PassengerCapacity").HasDefaultValue(1);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Driver)
                .WithMany(e => e.DriverVehicles)
                .HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_DriverVehicles_Drivers");

            entity.HasOne(e => e.TransportMode)
                .WithMany(e => e.DriverVehicles)
                .HasForeignKey(e => e.TransportModeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_DriverVehicles_TransportModes");

            entity.HasOne(e => e.TricyclePoint)
                .WithMany(e => e.DriverVehicles)
                .HasForeignKey(e => e.TricyclePointId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DriverVehicles_TricyclePoints");
        });
    }

    private static void ConfigureDriverLocations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DriverLocation>(entity =>
        {
            entity.ToTable("DriverLocations", "dbo");
            entity.HasKey(e => e.DriverLocationId);

            entity.HasIndex(e => e.DriverId, "UX_DriverLocations_Driver").IsUnique();
            entity.HasIndex(e => new { e.DriverId, e.UpdatedAt }, "IX_DriverLocations_DriverAndRecordedAt")
                .IsDescending(false, true);

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("RecordedAt")
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SpeedKph).HasColumnName("SpeedKilometersPerHour");

            entity.HasOne(e => e.Driver)
                .WithMany(e => e.DriverLocations)
                .HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DriverLocations_Drivers");
        });
    }

    private static void ConfigureDriverAvailabilitySessions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DriverAvailabilitySession>(entity =>
        {
            entity.ToTable("DriverAvailabilitySessions", "dbo");
            entity.HasKey(e => e.SessionId);

            entity.HasIndex(e => e.DriverId, "IX_DriverAvailability_Driver");
            entity.HasIndex(e => new { e.TricyclePointId, e.Status }, "IX_DriverAvailability_PointAndStatus");
            entity.HasIndex(e => new { e.DriverId, e.Status, e.EndedAt }, "IX_DriverAvailability_DriverAndStatus");

            entity.Property(e => e.SessionId).HasColumnName("DriverAvailabilitySessionId");
            entity.Property(e => e.VehicleId).HasColumnName("DriverVehicleId");
            entity.Property(e => e.DestinationName).HasMaxLength(250);
            entity.Property(e => e.MaximumDetourMeters).HasColumnType("decimal(10, 2)").HasDefaultValue(1000m);
            entity.Property(e => e.AvailableSeats).HasDefaultValue(1);
            entity.Property(e => e.StartedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("AVAILABLE");

            entity.HasOne(e => e.Driver)
                .WithMany(e => e.DriverAvailabilitySessions)
                .HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_DriverAvailability_Drivers");

            entity.HasOne(e => e.Vehicle)
                .WithMany(e => e.DriverAvailabilitySessions)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DriverAvailability_Vehicles");

            entity.HasOne(e => e.DestinationStop)
                .WithMany(e => e.DriverAvailabilitySessions)
                .HasForeignKey(e => e.DestinationStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DriverAvailability_DestinationStop");

            entity.HasOne(e => e.TricyclePoint)
                .WithMany(e => e.DriverAvailabilitySessions)
                .HasForeignKey(e => e.TricyclePointId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_DriverAvailability_TricyclePoints");
        });
    }

    private static void ConfigurePassengerRideRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PassengerRideRequest>(entity =>
        {
            entity.ToTable("PassengerRideRequests", "dbo");
            entity.HasKey(e => e.RequestId);

            entity.HasIndex(e => new { e.Status, e.RequestedAt }, "IX_PassengerRideRequests_Status");
            entity.HasIndex(e => e.PassengerUserId, "IX_PassengerRideRequests_Passenger");
            entity.HasIndex(e => e.TransportModeId, "IX_PassengerRideRequests_TransportMode");

            entity.Property(e => e.RequestId).HasColumnName("PassengerRideRequestId").HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.PickupName).HasMaxLength(250);
            entity.Property(e => e.DropoffName).HasMaxLength(250);
            entity.Property(e => e.DropoffLatitude).HasColumnName("DestinationLatitude");
            entity.Property(e => e.DropoffLongitude).HasColumnName("DestinationLongitude");
            entity.Property(e => e.MaxBudget).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.EstimatedFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.PassengerCount).HasDefaultValue(1);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("SEARCHING");

            entity.Ignore(e => e.DestinationLatitude);
            entity.Ignore(e => e.DestinationLongitude);

            entity.HasOne(e => e.PassengerUser)
                .WithMany(e => e.PassengerRideRequests)
                .HasForeignKey(e => e.PassengerUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RideRequests_UserProfiles");

            entity.HasOne(e => e.TransportMode)
                .WithMany(e => e.PassengerRideRequests)
                .HasForeignKey(e => e.TransportModeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_RideRequests_TransportModes");

            entity.HasOne(e => e.TricyclePoint)
                .WithMany(e => e.PassengerRideRequests)
                .HasForeignKey(e => e.TricyclePointId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_RideRequests_TricyclePoints");
        });
    }

    private static void ConfigureRideMatches(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RideMatch>(entity =>
        {
            entity.ToTable("RideMatches", "dbo");
            entity.HasKey(e => e.MatchId);

            entity.HasIndex(e => new { e.DriverId, e.Status, e.OfferedAt }, "IX_RideMatches_DriverAndStatus");
            entity.HasIndex(e => e.RequestId, "IX_RideMatches_Request");
            entity.HasIndex(e => new { e.RequestId, e.DriverId, e.SessionId }, "UX_RideMatches_RequestDriverSession")
                .IsUnique()
                .HasFilter("([SessionId] IS NOT NULL)");

            entity.Property(e => e.MatchId).HasColumnName("RideMatchId").HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.RequestId).HasColumnName("PassengerRideRequestId");
            entity.Property(e => e.VehicleId).HasColumnName("DriverVehicleId");
            entity.Property(e => e.PickupDistanceMeters).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.DetourDistanceMeters).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.EstimatedPickupMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.EstimatedTripMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.EstimatedFare).HasColumnName("OfferedFare").HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MatchScore).HasColumnType("decimal(12, 6)");
            entity.Property(e => e.OfferedAt).HasColumnName("MatchedAt").HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.AcceptedAt).HasColumnName("RespondedAt");
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("OFFERED");

            entity.Ignore(e => e.OfferedFare);
            entity.Ignore(e => e.MatchedAt);
            entity.Ignore(e => e.RespondedAt);

            entity.HasOne(e => e.Driver)
                .WithMany(e => e.RideMatches)
                .HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RideMatches_Drivers");

            entity.HasOne(e => e.Vehicle)
                .WithMany(e => e.RideMatches)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_RideMatches_Vehicles");

            entity.HasOne(e => e.Request)
                .WithMany(e => e.RideMatches)
                .HasForeignKey(e => e.RequestId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RideMatches_RideRequests");

            entity.HasOne(e => e.Session)
                .WithMany(e => e.RideMatches)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_RideMatches_AvailabilitySessions");
        });
    }

    private static void ConfigureTripSearches(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TripSearch>(entity =>
        {
            entity.ToTable("TripSearches", "dbo");
            entity.HasKey(e => e.TripSearchId);

            entity.HasIndex(e => e.UserId, "IX_TripSearches_User");

            entity.Property(e => e.TripSearchId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.OriginName).HasMaxLength(250);
            entity.Property(e => e.DestinationName).HasMaxLength(250);
            entity.Property(e => e.Budget).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.PassengerCount).HasDefaultValue(1);
            entity.Property(e => e.Preference).HasMaxLength(30);
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.User)
                .WithMany(e => e.TripSearches)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TripSearches_UserProfiles");
        });
    }

    private static void ConfigureRouteRecommendations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteRecommendation>(entity =>
        {
            entity.ToTable("RouteRecommendations", "dbo");
            entity.HasKey(e => e.RecommendationId);

            entity.HasIndex(e => e.TripSearchId, "IX_RouteRecommendations_TripSearch");
            entity.HasIndex(e => new { e.TripSearchId, e.RecommendationType, e.RankNumber }, "UX_RouteRecommendations_SearchTypeRank").IsUnique();

            entity.Property(e => e.RecommendationId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.RecommendationType).HasMaxLength(30);
            entity.Property(e => e.TotalFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalDistanceMeters).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.WalkingDistanceMeters).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.TransferCount).HasDefaultValue(0);
            entity.Property(e => e.RecommendationScore).HasColumnType("decimal(12, 6)");
            entity.Property(e => e.RankNumber).HasDefaultValue(1);
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.TripSearch)
                .WithMany(e => e.RouteRecommendations)
                .HasForeignKey(e => e.TripSearchId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RouteRecommendations_TripSearches");
        });
    }

    private static void ConfigureRecommendationLegs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecommendationLeg>(entity =>
        {
            entity.ToTable("RecommendationLegs", "dbo");
            entity.HasKey(e => e.LegId);

            entity.HasIndex(e => new { e.RecommendationId, e.LegOrder }, "IX_RecommendationLegs_Recommendation");
            entity.HasIndex(e => new { e.RecommendationId, e.LegOrder }, "UX_RecommendationLegs_RecommendationOrder").IsUnique();
            entity.HasIndex(e => e.TransportModeId, "IX_RecommendationLegs_TransportMode");
            entity.HasIndex(e => e.RouteId, "IX_RecommendationLegs_Route")
                .HasFilter("([TransportRouteId] IS NOT NULL)");

            entity.Property(e => e.LegId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.RouteId).HasColumnName("TransportRouteId");
            entity.Property(e => e.FromStopId).HasColumnName("FromTransportStopId");
            entity.Property(e => e.ToStopId).HasColumnName("ToTransportStopId");
            entity.Property(e => e.FromName).HasMaxLength(250);
            entity.Property(e => e.ToName).HasMaxLength(250);
            entity.Property(e => e.DistanceMeters).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.EstimatedMinutes).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.EstimatedFare).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Recommendation)
                .WithMany(e => e.RecommendationLegs)
                .HasForeignKey(e => e.RecommendationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RecommendationLegs_RouteRecommendations");

            entity.HasOne(e => e.TransportMode)
                .WithMany(e => e.RecommendationLegs)
                .HasForeignKey(e => e.TransportModeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RecommendationLegs_TransportModes");

            entity.HasOne(e => e.Route)
                .WithMany(e => e.RecommendationLegs)
                .HasForeignKey(e => e.RouteId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_RecommendationLegs_TransportRoutes");

            entity.HasOne(e => e.FromStop)
                .WithMany(e => e.RecommendationLegsStartingHere)
                .HasForeignKey(e => e.FromStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_RecommendationLegs_FromStop");

            entity.HasOne(e => e.ToStop)
                .WithMany(e => e.RecommendationLegsEndingHere)
                .HasForeignKey(e => e.ToStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_RecommendationLegs_ToStop");
        });
    }

    private static void ConfigurePassengerTrips(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PassengerTrip>(entity =>
        {
            entity.ToTable("PassengerTrips", "dbo");
            entity.HasKey(e => e.PassengerTripId);

            entity.HasIndex(e => e.UserId, "IX_PassengerTrips_User");

            entity.Property(e => e.PassengerTripId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CurrentLegOrder).HasDefaultValue(1);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("PLANNED");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.User)
                .WithMany(e => e.PassengerTrips)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PassengerTrips_UserProfiles");

            entity.HasOne(e => e.Recommendation)
                .WithMany(e => e.PassengerTrips)
                .HasForeignKey(e => e.RecommendationId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PassengerTrips_RouteRecommendations");
        });
    }

    private static void ConfigureTripAlerts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TripAlert>(entity =>
        {
            entity.ToTable("TripAlerts", "dbo");
            entity.HasKey(e => e.AlertId);

            entity.HasIndex(e => e.PassengerTripId, "IX_TripAlerts_PassengerTrip");

            entity.Property(e => e.AlertId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.TargetStopId).HasColumnName("TargetTransportStopId");
            entity.Property(e => e.AlertType).HasMaxLength(40);
            entity.Property(e => e.Title).HasMaxLength(150);
            entity.Property(e => e.TriggerDistanceMeters).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.IsTriggered).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.PassengerTrip)
                .WithMany(e => e.TripAlerts)
                .HasForeignKey(e => e.PassengerTripId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_TripAlerts_PassengerTrips");

            entity.HasOne(e => e.Leg)
                .WithMany(e => e.TripAlerts)
                .HasForeignKey(e => e.LegId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TripAlerts_RecommendationLegs");

            entity.HasOne(e => e.TargetStop)
                .WithMany(e => e.TripAlerts)
                .HasForeignKey(e => e.TargetStopId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_TripAlerts_TargetStop");
        });
    }

    private static void ConfigureChatConversations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.ToTable("ChatConversations", "dbo");
            entity.HasKey(e => e.ConversationId);

            entity.HasIndex(e => e.UserId, "IX_ChatConversations_User");

            entity.Property(e => e.ConversationId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.User)
                .WithMany(e => e.ChatConversations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ChatConversations_UserProfiles");
        });
    }

    private static void ConfigureChatMessages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages", "dbo");
            entity.HasKey(e => e.MessageId);

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "IX_ChatMessages_Conversation");

            entity.Property(e => e.MessageId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Sender).HasMaxLength(20);
            entity.Property(e => e.ExtractedBudget).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ExtractedOrigin).HasMaxLength(250);
            entity.Property(e => e.ExtractedDestination).HasMaxLength(250);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Conversation)
                .WithMany(e => e.ChatMessages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChatMessages_ChatConversations");

            entity.HasOne(e => e.TripSearch)
                .WithMany(e => e.ChatMessages)
                .HasForeignKey(e => e.TripSearchId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ChatMessages_TripSearches");
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
