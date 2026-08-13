using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace backend.Models.Database;

/*
 * EF Core cannot scaffold Supabase's gis.geography(...) columns because PostGIS was installed in
 * the custom gis schema. Keep these properties here instead of the generated entity files so a
 * future scaffold does not remove them.
 *
 * Point uses X = longitude and Y = latitude. SRID 4326 is the standard GPS coordinate system.
 */
public partial class transport_stop
{
    public Point? location { get; set; }
}

public partial class route_segment
{
    public LineString? geometry { get; set; }
}

public partial class trip_search
{
    public Point? origin_location { get; set; }
    public Point? destination_location { get; set; }
}

public partial class driver_location
{
    public Point? location { get; set; }
}

public partial class driver_availability_session
{
    public LineString? route_geometry { get; set; }
}

public partial class passenger_ride_request
{
    public Point? pickup_location { get; set; }
    public Point? dropoff_location { get; set; }
}

public partial class SupabaseDbContext
{
    // This implements the partial hook generated at the end of SupabaseDbContext.cs.
    // Use an unqualified type name because Program.cs adds gis to the database search path.
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<transport_stop>()
            .Property(entity => entity.location)
            .HasColumnType("geography(Point,4326)");

        modelBuilder.Entity<route_segment>()
            .Property(entity => entity.geometry)
            .HasColumnType("geography(LineString,4326)");

        modelBuilder.Entity<trip_search>()
            .Property(entity => entity.origin_location)
            .HasColumnType("geography(Point,4326)");
        modelBuilder.Entity<trip_search>()
            .Property(entity => entity.destination_location)
            .HasColumnType("geography(Point,4326)");

        modelBuilder.Entity<driver_location>()
            .Property(entity => entity.location)
            .HasColumnType("geography(Point,4326)");

        modelBuilder.Entity<driver_availability_session>()
            .Property(entity => entity.route_geometry)
            .HasColumnType("geography(LineString,4326)");

        modelBuilder.Entity<passenger_ride_request>()
            .Property(entity => entity.pickup_location)
            .HasColumnType("geography(Point,4326)");
        modelBuilder.Entity<passenger_ride_request>()
            .Property(entity => entity.dropoff_location)
            .HasColumnType("geography(Point,4326)");
    }
}
