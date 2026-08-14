using backend.Models.Database;
using backend.Repositories;
using NetTopologySuite.Geometries;

namespace backend.Services;

public sealed class DriverService(
    IDriverRepository driverRepository,
    IDriverVehicleRepository driverVehicleRepository,
    IDriverLocationRepository driverLocationRepository,
    IDriverAvailabilitySessionRepository availabilitySessionRepository) : IDriverService
{
    private const string AvailableStatus = "AVAILABLE";
    private const int Wgs84Srid = 4326;

    private readonly IDriverRepository _driverRepository = driverRepository;
    private readonly IDriverVehicleRepository _driverVehicleRepository = driverVehicleRepository;
    private readonly IDriverLocationRepository _driverLocationRepository = driverLocationRepository;
    private readonly IDriverAvailabilitySessionRepository _availabilitySessionRepository = availabilitySessionRepository;

    public Task<Driver?> GetDriverByIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return Task.FromResult<Driver?>(null);
        }

        return _driverRepository.GetByIdAsync(driverId, cancellationToken);
    }

    public Task<Driver?> GetDriverByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult<Driver?>(null);
        }

        return _driverRepository.GetByUserIdAsync(userId, cancellationToken);
    }

    public async Task<DriverDetailsDto?> GetDriverDetailsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return null;
        }

        var driver = await _driverRepository.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        // Details coordinate the driver repositories into one controller-ready result.
        var driverWithHomeTerminal = await _driverRepository.GetWithHomeTerminalAsync(driverId, cancellationToken);
        var activeVehicles = await _driverVehicleRepository.GetActiveByDriverAsync(driverId, cancellationToken);
        var currentLocation = await _driverLocationRepository.GetByDriverAsync(driverId, cancellationToken);
        var currentAvailabilitySession = await _availabilitySessionRepository.GetActiveByDriverAsync(driverId, cancellationToken);

        return MapDriverDetails(
            driver,
            driverWithHomeTerminal?.HomeTerminal,
            activeVehicles,
            currentLocation,
            currentAvailabilitySession);
    }

    public Task<List<DriverVehicle>> GetDriverVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return Task.FromResult(new List<DriverVehicle>());
        }

        return _driverVehicleRepository.GetByDriverAsync(driverId, cancellationToken);
    }

    public Task<List<DriverVehicle>> GetActiveDriverVehiclesAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return Task.FromResult(new List<DriverVehicle>());
        }

        return _driverVehicleRepository.GetActiveByDriverAsync(driverId, cancellationToken);
    }

    public Task<bool> SetDriverAvailabilityAsync(Guid driverId, bool isAvailable, CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return _driverRepository.UpdateAvailabilityAsync(driverId, isAvailable, cancellationToken);
    }

    public Task<DriverLocation?> GetDriverLocationAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return Task.FromResult<DriverLocation?>(null);
        }

        return _driverLocationRepository.GetByDriverAsync(driverId, cancellationToken);
    }

    public async Task<DriverLocation?> UpdateDriverLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        decimal? headingDegrees = null,
        decimal? speedKph = null,
        decimal? accuracyMeters = null,
        DateTime? updatedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty || !IsValidCoordinate(latitude, longitude))
        {
            return null;
        }

        var driver = await _driverRepository.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        var driverLocation = new DriverLocation
        {
            DriverId = driverId,
            Latitude = latitude,
            Longitude = longitude,
            Location = CreatePoint(latitude, longitude),
            HeadingDegrees = headingDegrees,
            SpeedKph = speedKph,
            AccuracyMeters = accuracyMeters,
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
        };

        return await _driverLocationRepository.AddOrUpdateAsync(driverLocation, cancellationToken);
    }

    public Task<DriverAvailabilitySession?> GetActiveAvailabilitySessionAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return Task.FromResult<DriverAvailabilitySession?>(null);
        }

        return _availabilitySessionRepository.GetActiveByDriverAsync(driverId, cancellationToken);
    }

    public async Task<DriverAvailabilitySession?> StartAvailabilitySessionAsync(
        Guid driverId,
        Guid? vehicleId = null,
        Guid? destinationStopId = null,
        string? destinationName = null,
        double? destinationLatitude = null,
        double? destinationLongitude = null,
        int availableSeats = 1,
        decimal maximumDetourMeters = 1000,
        DateTime? startedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty ||
            availableSeats <= 0 ||
            maximumDetourMeters < 0 ||
            !HasValidOptionalCoordinate(destinationLatitude, destinationLongitude))
        {
            return null;
        }

        var driver = await _driverRepository.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        var vehicle = await GetValidatedVehicleAsync(driverId, vehicleId, availableSeats, cancellationToken);
        if (vehicleId.HasValue && vehicle is null)
        {
            return null;
        }

        var activeSession = await _availabilitySessionRepository.GetActiveByDriverAsync(driverId, cancellationToken);
        if (activeSession is not null)
        {
            return null;
        }

        var session = new DriverAvailabilitySession
        {
            DriverId = driverId,
            VehicleId = vehicleId,
            DestinationStopId = destinationStopId,
            DestinationName = NormalizeOptionalText(destinationName),
            DestinationLatitude = destinationLatitude,
            DestinationLongitude = destinationLongitude,
            AvailableSeats = availableSeats,
            MaximumDetourMeters = maximumDetourMeters,
            Status = AvailableStatus,
            StartedAt = startedAt ?? DateTime.UtcNow,
        };

        var createdSession = await _availabilitySessionRepository.AddAsync(session, cancellationToken);
        await _driverRepository.UpdateAvailabilityAsync(driverId, true, cancellationToken);

        return createdSession;
    }

    public async Task<bool> EndAvailabilitySessionAsync(
        Guid driverId,
        DateTime? endedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return false;
        }

        var activeSession = await _availabilitySessionRepository.GetActiveByDriverAsync(driverId, cancellationToken);
        if (activeSession is null)
        {
            return false;
        }

        var sessionEnded = await _availabilitySessionRepository.EndSessionAsync(
            activeSession.SessionId,
            endedAt,
            cancellationToken);
        if (!sessionEnded)
        {
            return false;
        }

        return await _driverRepository.UpdateAvailabilityAsync(driverId, false, cancellationToken);
    }

    private async Task<DriverVehicle?> GetValidatedVehicleAsync(
        Guid driverId,
        Guid? vehicleId,
        int requestedSeats,
        CancellationToken cancellationToken)
    {
        if (!vehicleId.HasValue)
        {
            return null;
        }

        var vehicle = await _driverVehicleRepository.GetByIdAsync(vehicleId.Value, cancellationToken);

        // Starting availability with a vehicle requires an active vehicle owned by the driver.
        if (vehicle is null ||
            vehicle.DriverId != driverId ||
            !vehicle.IsActive ||
            requestedSeats > vehicle.Capacity)
        {
            return null;
        }

        return vehicle;
    }

    private static DriverDetailsDto MapDriverDetails(
        Driver driver,
        TransportStop? homeTerminal,
        IReadOnlyList<DriverVehicle> activeVehicles,
        DriverLocation? currentLocation,
        DriverAvailabilitySession? currentAvailabilitySession) =>
        new(
            driver.DriverId,
            driver.UserId,
            MapUserProfile(driver.User),
            driver.LicenseNumber,
            driver.VerificationStatus,
            driver.HomeTerminalId,
            MapTransportStop(homeTerminal),
            driver.AverageRating,
            driver.RatingCount,
            driver.IsAvailable,
            driver.CreatedAt,
            driver.UpdatedAt,
            activeVehicles.Select(MapDriverVehicle).ToList(),
            MapDriverLocation(currentLocation),
            MapAvailabilitySession(currentAvailabilitySession));

    private static DriverUserProfileDto? MapUserProfile(UserProfile? user) =>
        user is null
            ? null
            : new DriverUserProfileDto(
                user.UserId,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.Role,
                user.ProfileImageUrl,
                user.IsActive);

    private static DriverVehicleDto MapDriverVehicle(DriverVehicle vehicle) =>
        new(
            vehicle.VehicleId,
            vehicle.DriverId,
            vehicle.TransportModeId,
            MapTransportMode(vehicle.TransportMode),
            vehicle.PlateNumber,
            vehicle.BodyNumber,
            vehicle.Color,
            vehicle.Capacity,
            vehicle.IsActive,
            vehicle.CreatedAt);

    private static DriverLocationDto? MapDriverLocation(DriverLocation? location) =>
        location is null
            ? null
            : new DriverLocationDto(
                location.DriverId,
                location.Latitude,
                location.Longitude,
                location.HeadingDegrees,
                location.SpeedKph,
                location.AccuracyMeters,
                location.UpdatedAt);

    private static DriverAvailabilitySessionDto? MapAvailabilitySession(DriverAvailabilitySession? session) =>
        session is null
            ? null
            : new DriverAvailabilitySessionDto(
                session.SessionId,
                session.DriverId,
                session.VehicleId,
                session.Vehicle is null ? null : MapDriverVehicle(session.Vehicle),
                session.DestinationStopId,
                MapTransportStop(session.DestinationStop),
                session.DestinationName,
                session.DestinationLatitude,
                session.DestinationLongitude,
                session.AvailableSeats,
                session.MaximumDetourMeters,
                session.Status,
                session.StartedAt,
                session.EndedAt);

    private static TransportModeSummaryDto? MapTransportMode(TransportMode? mode) =>
        mode is null
            ? null
            : new TransportModeSummaryDto(
                mode.TransportModeId,
                mode.Code,
                mode.Name,
                mode.IsMotorized,
                mode.AllowsLiveDriver,
                mode.IconName);

    private static TransportStopSummaryDto? MapTransportStop(TransportStop? stop) =>
        stop is null
            ? null
            : new TransportStopSummaryDto(
                stop.StopId,
                stop.StopCode,
                stop.Name,
                stop.Description,
                stop.StopType,
                stop.Address,
                stop.Latitude,
                stop.Longitude);

    private static Point CreatePoint(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = Wgs84Srid };

    private static bool IsValidCoordinate(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;

    private static bool HasValidOptionalCoordinate(double? latitude, double? longitude) =>
        (latitude is null && longitude is null) ||
        (latitude.HasValue && longitude.HasValue && IsValidCoordinate(latitude.Value, longitude.Value));

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
