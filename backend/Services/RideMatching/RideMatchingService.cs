using backend.Models.Database;
using backend.Repositories;
using NetTopologySuite.Geometries;

namespace backend.Services;

public sealed class RideMatchingService(
    IPassengerRideRequestRepository rideRequestRepository,
    IRideMatchRepository rideMatchRepository,
    IDriverRepository driverRepository,
    IDriverLocationRepository driverLocationRepository,
    IDriverAvailabilitySessionRepository availabilitySessionRepository,
    IDriverVehicleRepository driverVehicleRepository,
    ITransportModeRepository transportModeRepository) : IRideMatchingService
{
    private const string SearchingRequestStatus = "SEARCHING";
    private const string MatchedRequestStatus = "MATCHED";
    private const string OfferedMatchStatus = "OFFERED";
    private const string AcceptedMatchStatus = "ACCEPTED";
    private const string RejectedMatchStatus = "REJECTED";
    private const string CancelledMatchStatus = "CANCELLED";
    private const int Wgs84Srid = 4326;

    private readonly IPassengerRideRequestRepository _rideRequestRepository = rideRequestRepository;
    private readonly IRideMatchRepository _rideMatchRepository = rideMatchRepository;
    private readonly IDriverRepository _driverRepository = driverRepository;
    private readonly IDriverLocationRepository _driverLocationRepository = driverLocationRepository;
    private readonly IDriverAvailabilitySessionRepository _availabilitySessionRepository = availabilitySessionRepository;
    private readonly IDriverVehicleRepository _driverVehicleRepository = driverVehicleRepository;
    private readonly ITransportModeRepository _transportModeRepository = transportModeRepository;

    public Task<PassengerRideRequest?> GetRideRequestByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return Task.FromResult<PassengerRideRequest?>(null);
        }

        return _rideRequestRepository.GetByIdAsync(requestId, cancellationToken);
    }

    public Task<List<PassengerRideRequest>> GetRideRequestsByPassengerAsync(
        Guid passengerUserId,
        CancellationToken cancellationToken = default)
    {
        if (passengerUserId == Guid.Empty)
        {
            return Task.FromResult(new List<PassengerRideRequest>());
        }

        return _rideRequestRepository.GetByPassengerAsync(passengerUserId, cancellationToken);
    }

    public Task<List<PassengerRideRequest>> GetActiveRideRequestsAsync(CancellationToken cancellationToken = default) =>
        _rideRequestRepository.GetActiveSearchingAsync(cancellationToken);

    public async Task<PassengerRideRequest?> CreateRideRequestAsync(
        Guid passengerUserId,
        string pickupName,
        double pickupLatitude,
        double pickupLongitude,
        string dropoffName,
        double dropoffLatitude,
        double dropoffLongitude,
        int passengerCount = 1,
        short? transportModeId = null,
        decimal? maxBudget = null,
        DateTime? requestedAt = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPickup = NormalizeRequiredText(pickupName);
        var normalizedDropoff = NormalizeRequiredText(dropoffName);
        if (passengerUserId == Guid.Empty ||
            normalizedPickup is null ||
            normalizedDropoff is null ||
            passengerCount <= 0 ||
            maxBudget < 0 ||
            !IsValidCoordinate(pickupLatitude, pickupLongitude) ||
            !IsValidCoordinate(dropoffLatitude, dropoffLongitude) ||
            (expiresAt.HasValue && expiresAt.Value <= (requestedAt ?? DateTime.UtcNow)) ||
            !await IsValidTransportModeAsync(transportModeId, cancellationToken))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var request = new PassengerRideRequest
        {
            PassengerUserId = passengerUserId,
            TransportModeId = transportModeId,
            PickupName = normalizedPickup,
            PickupLatitude = pickupLatitude,
            PickupLongitude = pickupLongitude,
            PickupLocation = CreatePoint(pickupLatitude, pickupLongitude),
            DropoffName = normalizedDropoff,
            DropoffLatitude = dropoffLatitude,
            DropoffLongitude = dropoffLongitude,
            DropoffLocation = CreatePoint(dropoffLatitude, dropoffLongitude),
            PassengerCount = passengerCount,
            MaxBudget = maxBudget,
            Status = SearchingRequestStatus,
            RequestedAt = requestedAt ?? now,
            ExpiresAt = expiresAt,
            UpdatedAt = now,
        };

        return await _rideRequestRepository.AddAsync(request, cancellationToken);
    }

    public async Task<List<DriverCandidateDto>> GetAvailableDriversAsync(CancellationToken cancellationToken = default)
    {
        var availableDrivers = await _driverRepository.GetAvailableDriversAsync(cancellationToken);
        var candidates = new List<DriverCandidateDto>();

        foreach (var driver in availableDrivers)
        {
            var candidate = await BuildCandidateAsync(driver, request: null, vehicleId: null, cancellationToken);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    public async Task<List<DriverCandidateDto>> GetCandidateDriversAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await GetActiveRideRequestAsync(requestId, cancellationToken);
        if (request is null)
        {
            return [];
        }

        var availableDrivers = await _driverRepository.GetAvailableDriversAsync(cancellationToken);
        var candidates = new List<DriverCandidateDto>();

        foreach (var driver in availableDrivers)
        {
            var candidate = await BuildCandidateAsync(driver, request, vehicleId: null, cancellationToken);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    public async Task<RideMatch?> CreateRideMatchAsync(
        Guid requestId,
        Guid driverId,
        Guid? vehicleId = null,
        decimal? pickupDistanceMeters = null,
        decimal? detourDistanceMeters = null,
        decimal? estimatedPickupMinutes = null,
        decimal? estimatedTripMinutes = null,
        decimal? estimatedFare = null,
        decimal? matchScore = null,
        DateTime? offeredAt = null,
        CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty ||
            driverId == Guid.Empty ||
            vehicleId == Guid.Empty ||
            !AreNonNegative(
                pickupDistanceMeters,
                detourDistanceMeters,
                estimatedPickupMinutes,
                estimatedTripMinutes,
                estimatedFare,
                matchScore))
        {
            return null;
        }

        var request = await GetActiveRideRequestAsync(requestId, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var driver = await _driverRepository.GetByIdAsync(driverId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        var candidate = await BuildCandidateAsync(driver, request, vehicleId, cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var requestMatches = await _rideMatchRepository.GetByRequestAsync(requestId, cancellationToken);
        if (requestMatches.Any(match =>
                match.DriverId == driverId &&
                match.SessionId == candidate.ActiveAvailabilitySession.SessionId))
        {
            return null;
        }

        var match = new RideMatch
        {
            RequestId = requestId,
            DriverId = driverId,
            SessionId = candidate.ActiveAvailabilitySession.SessionId,
            VehicleId = candidate.Vehicle.VehicleId,
            PickupDistanceMeters = pickupDistanceMeters,
            DetourDistanceMeters = detourDistanceMeters,
            EstimatedPickupMinutes = estimatedPickupMinutes,
            EstimatedTripMinutes = estimatedTripMinutes,
            EstimatedFare = estimatedFare,
            MatchScore = matchScore,
            Status = OfferedMatchStatus,
            OfferedAt = offeredAt ?? DateTime.UtcNow,
        };

        return await _rideMatchRepository.AddAsync(match, cancellationToken);
    }

    public Task<RideMatch?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (matchId == Guid.Empty)
        {
            return Task.FromResult<RideMatch?>(null);
        }

        return _rideMatchRepository.GetByIdAsync(matchId, cancellationToken);
    }

    public async Task<RideMatchDetailsDto?> GetMatchDetailsAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        if (matchId == Guid.Empty)
        {
            return null;
        }

        var match = await _rideMatchRepository.GetByIdAsync(matchId, cancellationToken);
        return match is null ? null : MapRideMatchDetails(match);
    }

    public Task<List<RideMatch>> GetMatchesForRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            return Task.FromResult(new List<RideMatch>());
        }

        return _rideMatchRepository.GetByRequestAsync(requestId, cancellationToken);
    }

    public Task<List<RideMatch>> GetMatchesForDriverAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        if (driverId == Guid.Empty)
        {
            return Task.FromResult(new List<RideMatch>());
        }

        return _rideMatchRepository.GetByDriverAsync(driverId, cancellationToken);
    }

    public async Task<bool> AcceptMatchAsync(
        Guid matchId,
        DateTime? acceptedAt = null,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOfferedMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return false;
        }

        var statusUpdated = await UpdateMatchStatusAsync(
            match,
            AcceptedMatchStatus,
            acceptedAt: acceptedAt ?? DateTime.UtcNow,
            completedAt: match.CompletedAt,
            cancellationToken);
        if (!statusUpdated)
        {
            return false;
        }

        return await _rideRequestRepository.UpdateStatusAsync(
            match.RequestId,
            MatchedRequestStatus,
            cancellationToken);
    }

    public async Task<bool> RejectMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await GetOfferedMatchAsync(matchId, cancellationToken);
        if (match is null)
        {
            return false;
        }

        return await UpdateMatchStatusAsync(
            match,
            RejectedMatchStatus,
            acceptedAt: match.AcceptedAt,
            completedAt: match.CompletedAt,
            cancellationToken);
    }

    public async Task<bool> CancelMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        if (matchId == Guid.Empty)
        {
            return false;
        }

        var match = await _rideMatchRepository.GetByIdAsync(matchId, cancellationToken);
        if (match is null)
        {
            return false;
        }

        return await UpdateMatchStatusAsync(
            match,
            CancelledMatchStatus,
            acceptedAt: match.AcceptedAt,
            completedAt: match.CompletedAt,
            cancellationToken);
    }

    private async Task<PassengerRideRequest?> GetActiveRideRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            return null;
        }

        var request = await _rideRequestRepository.GetByIdAsync(requestId, cancellationToken);
        return IsActiveSearchRequest(request) ? request : null;
    }

    private async Task<DriverCandidateDto?> BuildCandidateAsync(
        Driver driver,
        PassengerRideRequest? request,
        Guid? vehicleId,
        CancellationToken cancellationToken)
    {
        if (!driver.IsAvailable)
        {
            return null;
        }

        var activeSession = await _availabilitySessionRepository.GetActiveByDriverAsync(driver.DriverId, cancellationToken);
        if (activeSession is null)
        {
            return null;
        }

        if (vehicleId.HasValue &&
            activeSession.VehicleId.HasValue &&
            vehicleId.Value != activeSession.VehicleId.Value)
        {
            return null;
        }

        var passengerCount = request?.PassengerCount ?? 1;
        if (activeSession.AvailableSeats < passengerCount)
        {
            return null;
        }

        var currentLocation = await _driverLocationRepository.GetByDriverAsync(driver.DriverId, cancellationToken);
        if (currentLocation is null)
        {
            return null;
        }

        var vehicle = await SelectEligibleVehicleAsync(driver.DriverId, request, activeSession, vehicleId, cancellationToken);
        if (vehicle is null)
        {
            return null;
        }

        return new DriverCandidateDto(
            driver.DriverId,
            driver.UserId,
            driver.VerificationStatus,
            driver.AverageRating,
            driver.RatingCount,
            driver.IsAvailable,
            MapDriverLocation(currentLocation),
            MapDriverAvailabilitySession(activeSession)!,
            MapDriverVehicle(vehicle));
    }

    private async Task<DriverVehicle?> SelectEligibleVehicleAsync(
        Guid driverId,
        PassengerRideRequest? request,
        DriverAvailabilitySession activeSession,
        Guid? vehicleId,
        CancellationToken cancellationToken)
    {
        var activeVehicles = await _driverVehicleRepository.GetActiveByDriverAsync(driverId, cancellationToken);
        var eligibleVehicles = activeVehicles
            .Where(vehicle => IsVehicleEligible(vehicle, request))
            .ToList();

        if (vehicleId.HasValue)
        {
            return eligibleVehicles.FirstOrDefault(vehicle => vehicle.VehicleId == vehicleId.Value);
        }

        if (activeSession.VehicleId.HasValue)
        {
            return eligibleVehicles.FirstOrDefault(vehicle => vehicle.VehicleId == activeSession.VehicleId.Value);
        }

        return eligibleVehicles.FirstOrDefault();
    }

    private static bool IsVehicleEligible(DriverVehicle vehicle, PassengerRideRequest? request)
    {
        if (!vehicle.IsActive)
        {
            return false;
        }

        if (request is null)
        {
            return vehicle.Capacity > 0;
        }

        if (vehicle.Capacity < request.PassengerCount)
        {
            return false;
        }

        return !request.TransportModeId.HasValue || vehicle.TransportModeId == request.TransportModeId.Value;
    }

    private async Task<RideMatch?> GetOfferedMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return null;
        }

        var match = await _rideMatchRepository.GetByIdAsync(matchId, cancellationToken);
        return IsStatus(match?.Status, OfferedMatchStatus) ? match : null;
    }

    private async Task<bool> UpdateMatchStatusAsync(
        RideMatch match,
        string status,
        DateTime? acceptedAt,
        DateTime? completedAt,
        CancellationToken cancellationToken)
    {
        var updatedMatch = new RideMatch
        {
            MatchId = match.MatchId,
            RequestId = match.RequestId,
            DriverId = match.DriverId,
            SessionId = match.SessionId,
            VehicleId = match.VehicleId,
            PickupDistanceMeters = match.PickupDistanceMeters,
            DetourDistanceMeters = match.DetourDistanceMeters,
            EstimatedPickupMinutes = match.EstimatedPickupMinutes,
            EstimatedTripMinutes = match.EstimatedTripMinutes,
            EstimatedFare = match.EstimatedFare,
            MatchScore = match.MatchScore,
            Status = status,
            OfferedAt = match.OfferedAt,
            AcceptedAt = acceptedAt,
            CompletedAt = completedAt,
        };

        await _rideMatchRepository.UpdateAsync(updatedMatch, cancellationToken);
        return true;
    }

    private async Task<bool> IsValidTransportModeAsync(short? transportModeId, CancellationToken cancellationToken)
    {
        if (!transportModeId.HasValue)
        {
            return true;
        }

        if (transportModeId.Value <= 0)
        {
            return false;
        }

        var transportMode = await _transportModeRepository.GetByIdAsync(transportModeId.Value, cancellationToken);
        return transportMode is not null && transportMode.IsActive;
    }

    private static bool IsActiveSearchRequest(PassengerRideRequest? request) =>
        request is not null &&
        IsStatus(request.Status, SearchingRequestStatus) &&
        (!request.ExpiresAt.HasValue || request.ExpiresAt.Value > DateTime.UtcNow);

    private static bool IsStatus(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool AreNonNegative(params decimal?[] values) =>
        values.All(value => !value.HasValue || value.Value >= 0);

    private static RideMatchDetailsDto MapRideMatchDetails(RideMatch match) =>
        new(
            match.MatchId,
            match.RequestId,
            match.DriverId,
            match.SessionId,
            match.VehicleId,
            match.PickupDistanceMeters,
            match.DetourDistanceMeters,
            match.EstimatedPickupMinutes,
            match.EstimatedTripMinutes,
            match.EstimatedFare,
            match.MatchScore,
            match.Status,
            match.OfferedAt,
            match.AcceptedAt,
            match.CompletedAt,
            MapRideRequest(match.Request),
            MapDriver(match.Driver),
            MapDriverAvailabilitySession(match.Session),
            match.Vehicle is null ? null : MapDriverVehicle(match.Vehicle));

    private static RideRequestDetailsDto? MapRideRequest(PassengerRideRequest? request) =>
        request is null
            ? null
            : new RideRequestDetailsDto(
                request.RequestId,
                request.PassengerUserId,
                request.TransportModeId,
                MapTransportMode(request.TransportMode),
                request.PickupName,
                request.PickupLatitude,
                request.PickupLongitude,
                request.DropoffName,
                request.DropoffLatitude,
                request.DropoffLongitude,
                request.PassengerCount,
                request.MaxBudget,
                request.Status,
                request.RequestedAt,
                request.ExpiresAt,
                request.UpdatedAt);

    private static DriverSummaryDto? MapDriver(Driver? driver) =>
        driver is null
            ? null
            : new DriverSummaryDto(
                driver.DriverId,
                driver.UserId,
                driver.LicenseNumber,
                driver.VerificationStatus,
                driver.HomeTerminalId,
                driver.AverageRating,
                driver.RatingCount,
                driver.IsAvailable,
                driver.CreatedAt,
                driver.UpdatedAt);

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

    private static DriverLocationDto MapDriverLocation(DriverLocation location) =>
        new(
            location.DriverId,
            location.Latitude,
            location.Longitude,
            location.HeadingDegrees,
            location.SpeedKph,
            location.AccuracyMeters,
            location.UpdatedAt);

    private static DriverAvailabilitySessionDto? MapDriverAvailabilitySession(DriverAvailabilitySession? session) =>
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

    private static string? NormalizeRequiredText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
