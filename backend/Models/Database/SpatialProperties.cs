using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace backend.Models.Database;

/*
 * EF Core cannot scaffold Supabase's gis.geography(...) columns because PostGIS was installed in
 * the custom gis schema. Keep these properties here instead of the generated entity files so a
 * future scaffold does not remove them.
 *
 * Point uses X = Longitude and Y = Latitude. SRID 4326 is the standard GPS coordinate system.
 */
public partial class TransportStop
{
    public Point? Location { get; set; }
}

public partial class RouteSegment
{
    public LineString? Geometry { get; set; }
}

public partial class TripSearch
{
    public Point? OriginLocation { get; set; }
    public Point? DestinationLocation { get; set; }
}

public partial class DriverLocation
{
    public Point? Location { get; set; }
}

public partial class DriverAvailabilitySession
{
    public LineString? RouteGeometry { get; set; }
}

public partial class PassengerRideRequest
{
    public Point? PickupLocation { get; set; }
    public Point? DropoffLocation { get; set; }
}

public partial class SupabaseDbContext
{
    // This implements the partial hook generated at the end of SupabaseDbContext.cs.
    // Use an unqualified type Name because Program.cs adds gis to the database search path.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransportStop>()
            .Property(entity => entity.Location)
            .HasColumnType("geography(Point,4326)");

        modelBuilder.Entity<RouteSegment>()
            .Property(entity => entity.Geometry)
            .HasColumnType("geography(LineString,4326)");

        modelBuilder.Entity<TripSearch>()
            .Property(entity => entity.OriginLocation)
            .HasColumnType("geography(Point,4326)");
        modelBuilder.Entity<TripSearch>()
            .Property(entity => entity.DestinationLocation)
            .HasColumnType("geography(Point,4326)");

        modelBuilder.Entity<DriverLocation>()
            .Property(entity => entity.Location)
            .HasColumnType("geography(Point,4326)");

        modelBuilder.Entity<DriverAvailabilitySession>()
            .Property(entity => entity.RouteGeometry)
            .HasColumnType("geography(LineString,4326)");

        modelBuilder.Entity<PassengerRideRequest>()
            .Property(entity => entity.PickupLocation)
            .HasColumnType("geography(Point,4326)");
        modelBuilder.Entity<PassengerRideRequest>()
            .Property(entity => entity.DropoffLocation)
            .HasColumnType("geography(Point,4326)");
    }
}
