using backend.Models.Database;
using backend.Repositories;

namespace backend.Services;

public sealed class TripService(
    ITripSearchRepository tripSearchRepository,
    IRouteRecommendationRepository routeRecommendationRepository,
    IRecommendationLegRepository recommendationLegRepository,
    IPassengerTripRepository passengerTripRepository,
    ITripAlertRepository tripAlertRepository) : ITripService
{
    private const string InProgressStatus = "IN_PROGRESS";
    private const int InitialLegOrder = 1;
    private readonly ITripSearchRepository _tripSearchRepository = tripSearchRepository;
    private readonly IRouteRecommendationRepository _routeRecommendationRepository = routeRecommendationRepository;
    private readonly IRecommendationLegRepository _recommendationLegRepository = recommendationLegRepository;
    private readonly IPassengerTripRepository _passengerTripRepository = passengerTripRepository;
    private readonly ITripAlertRepository _tripAlertRepository = tripAlertRepository;

    public Task<TripSearch?> GetTripSearchByIdAsync(Guid tripSearchId, CancellationToken cancellationToken = default)
    {
        if (tripSearchId == Guid.Empty)
        {
            return Task.FromResult<TripSearch?>(null);
        }

        return _tripSearchRepository.GetByIdAsync(tripSearchId, cancellationToken);
    }

    public Task<List<TripSearch>> GetTripSearchesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(new List<TripSearch>());
        }

        return _tripSearchRepository.GetByUserAsync(userId, cancellationToken);
    }

    public async Task<TripSearch?> CreateTripSearchAsync(
        Guid? userId,
        string originName,
        double originLatitude,
        double originLongitude,
        string destinationName,
        double destinationLatitude,
        double destinationLongitude,
        int passengerCount = 1,
        decimal? budget = null,
        string? preference = null,
        DateTime? requestedAt = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOrigin = NormalizeRequiredText(originName);
        var normalizedDestination = NormalizeRequiredText(destinationName);
        if (normalizedOrigin is null ||
            normalizedDestination is null ||
            (userId.HasValue && userId.Value == Guid.Empty) ||
            passengerCount <= 0 ||
            budget < 0 ||
            !IsValidCoordinate(originLatitude, originLongitude) ||
            !IsValidCoordinate(destinationLatitude, destinationLongitude))
        {
            return null;
        }

        var search = new TripSearch
        {
            UserId = userId,
            OriginName = normalizedOrigin,
            OriginLatitude = originLatitude,
            OriginLongitude = originLongitude,
            DestinationName = normalizedDestination,
            DestinationLatitude = destinationLatitude,
            DestinationLongitude = destinationLongitude,
            PassengerCount = passengerCount,
            Budget = budget,
            Preference = NormalizeOptionalText(preference),
            RequestedAt = requestedAt ?? DateTime.UtcNow,
        };

        return await _tripSearchRepository.AddAsync(search, cancellationToken);
    }

    public Task<List<RouteRecommendation>> GetRecommendationsForSearchAsync(
        Guid tripSearchId,
        CancellationToken cancellationToken = default)
    {
        if (tripSearchId == Guid.Empty)
        {
            return Task.FromResult(new List<RouteRecommendation>());
        }

        return _routeRecommendationRepository.GetByTripSearchAsync(tripSearchId, cancellationToken);
    }

    public Task<RouteRecommendation?> GetRecommendationByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        if (recommendationId == Guid.Empty)
        {
            return Task.FromResult<RouteRecommendation?>(null);
        }

        return _routeRecommendationRepository.GetByIdAsync(recommendationId, cancellationToken);
    }

    public async Task<RecommendationDetailsDto?> GetRecommendationDetailsAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        if (recommendationId == Guid.Empty)
        {
            return null;
        }

        var recommendation = await _routeRecommendationRepository.GetByIdAsync(recommendationId, cancellationToken);
        if (recommendation is null)
        {
            return null;
        }

        // Recommendation totals are stored values; legs are retrieved in repository-defined LegOrder.
        var legs = await _recommendationLegRepository.GetOrderedByRecommendationAsync(recommendationId, cancellationToken);

        return MapRecommendationDetails(recommendation, legs);
    }

    public Task<PassengerTrip?> GetPassengerTripByIdAsync(
        Guid passengerTripId,
        CancellationToken cancellationToken = default)
    {
        if (passengerTripId == Guid.Empty)
        {
            return Task.FromResult<PassengerTrip?>(null);
        }

        return _passengerTripRepository.GetByIdAsync(passengerTripId, cancellationToken);
    }

    public Task<List<PassengerTrip>> GetPassengerTripsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(new List<PassengerTrip>());
        }

        return _passengerTripRepository.GetByUserAsync(userId, cancellationToken);
    }

    public async Task<PassengerTrip?> StartPassengerTripAsync(
        Guid userId,
        Guid recommendationId,
        DateTime? startedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || recommendationId == Guid.Empty)
        {
            return null;
        }

        var recommendation = await _routeRecommendationRepository.GetByIdAsync(recommendationId, cancellationToken);
        if (recommendation is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var tripStartedAt = startedAt ?? now;
        var trip = new PassengerTrip
        {
            UserId = userId,
            RecommendationId = recommendationId,
            CurrentLegOrder = InitialLegOrder,
            Status = InProgressStatus,
            StartedAt = tripStartedAt,
            CreatedAt = now,
            UpdatedAt = now,
        };

        return await _passengerTripRepository.AddAsync(trip, cancellationToken);
    }

    public Task<bool> UpdatePassengerTripStatusAsync(
        Guid passengerTripId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = NormalizeRequiredText(status);
        if (passengerTripId == Guid.Empty || normalizedStatus is null)
        {
            return Task.FromResult(false);
        }

        return _passengerTripRepository.UpdateStatusAndCurrentLegAsync(
            passengerTripId,
            normalizedStatus,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateCurrentLegAsync(
        Guid passengerTripId,
        int currentLegOrder,
        CancellationToken cancellationToken = default)
    {
        if (passengerTripId == Guid.Empty || currentLegOrder <= 0)
        {
            return false;
        }

        var trip = await _passengerTripRepository.GetByIdAsync(passengerTripId, cancellationToken);
        if (trip is null)
        {
            return false;
        }

        return await _passengerTripRepository.UpdateStatusAndCurrentLegAsync(
            passengerTripId,
            trip.Status,
            currentLegOrder,
            cancellationToken);
    }

    public async Task<PassengerTripDetailsDto?> GetPassengerTripDetailsAsync(
        Guid passengerTripId,
        CancellationToken cancellationToken = default)
    {
        if (passengerTripId == Guid.Empty)
        {
            return null;
        }

        var trip = await _passengerTripRepository.GetByIdAsync(passengerTripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var recommendation = await GetRecommendationDetailsAsync(trip.RecommendationId, cancellationToken);
        var alerts = await _tripAlertRepository.GetByPassengerTripAsync(passengerTripId, cancellationToken);

        return MapPassengerTripDetails(trip, recommendation, alerts);
    }

    public Task<List<TripAlert>> GetTripAlertsAsync(Guid passengerTripId, CancellationToken cancellationToken = default)
    {
        if (passengerTripId == Guid.Empty)
        {
            return Task.FromResult(new List<TripAlert>());
        }

        return _tripAlertRepository.GetByPassengerTripAsync(passengerTripId, cancellationToken);
    }

    public async Task<List<TripAlert>> GetPendingTripAlertsAsync(
        Guid passengerTripId,
        CancellationToken cancellationToken = default)
    {
        if (passengerTripId == Guid.Empty)
        {
            return [];
        }

        var alerts = await _tripAlertRepository.GetByPassengerTripAsync(passengerTripId, cancellationToken);
        return alerts.Where(alert => !alert.IsTriggered).ToList();
    }

    public async Task<TripAlert?> CreateTripAlertAsync(
        Guid passengerTripId,
        string alertType,
        string message,
        Guid? legId = null,
        long? targetStopId = null,
        string? title = null,
        decimal? triggerDistanceMeters = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedAlertType = NormalizeRequiredText(alertType);
        var normalizedMessage = NormalizeRequiredText(message);
        if (passengerTripId == Guid.Empty ||
            normalizedAlertType is null ||
            normalizedMessage is null ||
            (legId.HasValue && legId.Value == Guid.Empty) ||
            (targetStopId.HasValue && targetStopId.Value <= 0) ||
            triggerDistanceMeters < 0)
        {
            return null;
        }

        var trip = await _passengerTripRepository.GetByIdAsync(passengerTripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        if (legId.HasValue && !await IsLegInPassengerTripRecommendationAsync(trip, legId.Value, cancellationToken))
        {
            return null;
        }

        var alert = new TripAlert
        {
            PassengerTripId = passengerTripId,
            LegId = legId,
            TargetStopId = targetStopId,
            AlertType = normalizedAlertType,
            Title = NormalizeOptionalText(title),
            Message = normalizedMessage,
            TriggerDistanceMeters = triggerDistanceMeters,
            IsTriggered = false,
            CreatedAt = DateTime.UtcNow,
        };

        return await _tripAlertRepository.AddAsync(alert, cancellationToken);
    }

    public Task<bool> MarkTripAlertTriggeredAsync(
        Guid alertId,
        DateTime? triggeredAt = null,
        CancellationToken cancellationToken = default)
    {
        if (alertId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return _tripAlertRepository.UpdateTriggerStateAsync(alertId, true, triggeredAt, cancellationToken);
    }

    private async Task<bool> IsLegInPassengerTripRecommendationAsync(
        PassengerTrip trip,
        Guid legId,
        CancellationToken cancellationToken)
    {
        var leg = await _recommendationLegRepository.GetByIdAsync(legId, cancellationToken);
        return leg is not null && leg.RecommendationId == trip.RecommendationId;
    }

    private static RecommendationDetailsDto MapRecommendationDetails(
        RouteRecommendation recommendation,
        IReadOnlyList<RecommendationLeg> legs) =>
        new(
            recommendation.RecommendationId,
            recommendation.TripSearchId,
            recommendation.RecommendationType,
            recommendation.RankNumber,
            recommendation.TotalFare,
            recommendation.TotalMinutes,
            recommendation.TotalDistanceMeters,
            recommendation.WalkingDistanceMeters,
            recommendation.TransferCount,
            recommendation.RecommendationScore,
            recommendation.Explanation,
            recommendation.GeneratedAt,
            legs.Select(MapRecommendationLeg).ToList());

    private static RecommendationLegDto MapRecommendationLeg(RecommendationLeg leg) =>
        new(
            leg.LegId,
            leg.RecommendationId,
            leg.LegOrder,
            leg.TransportModeId,
            MapTransportMode(leg.TransportMode),
            leg.RouteId,
            MapTransportRoute(leg.Route),
            leg.FromStopId,
            MapTransportStop(leg.FromStop),
            leg.ToStopId,
            MapTransportStop(leg.ToStop),
            leg.FromName,
            leg.ToName,
            leg.StartLatitude,
            leg.StartLongitude,
            leg.EndLatitude,
            leg.EndLongitude,
            leg.DistanceMeters,
            leg.EstimatedMinutes,
            leg.EstimatedFare,
            leg.Instructions,
            leg.CreatedAt);

    private static PassengerTripDetailsDto MapPassengerTripDetails(
        PassengerTrip trip,
        RecommendationDetailsDto? recommendation,
        IReadOnlyList<TripAlert> alerts) =>
        new(
            trip.PassengerTripId,
            trip.UserId,
            trip.RecommendationId,
            trip.CurrentLegOrder,
            trip.Status,
            trip.StartedAt,
            trip.CompletedAt,
            trip.CreatedAt,
            trip.UpdatedAt,
            recommendation,
            alerts.Select(MapTripAlert).ToList());

    private static TripAlertDto MapTripAlert(TripAlert alert) =>
        new(
            alert.AlertId,
            alert.PassengerTripId,
            alert.LegId,
            alert.TargetStopId,
            MapTransportStop(alert.TargetStop),
            alert.AlertType,
            alert.Title,
            alert.Message,
            alert.TriggerDistanceMeters,
            alert.IsTriggered,
            alert.TriggeredAt,
            alert.CreatedAt);

    private static TransportRouteSummaryDto? MapTransportRoute(TransportRoute? route) =>
        route is null
            ? null
            : new TransportRouteSummaryDto(
                route.RouteId,
                route.RouteCode,
                route.RouteName,
                route.TransportModeId,
                route.BaseFare,
                route.EstimatedTotalMinutes,
                route.IsActive);

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

    private static bool IsValidCoordinate(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;

    private static string? NormalizeRequiredText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalText(string? value) =>
        NormalizeRequiredText(value);
}
