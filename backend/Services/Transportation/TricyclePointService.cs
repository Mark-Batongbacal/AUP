using backend.Models.Database;
using backend.Repositories;
using backend.Services.Routing;

namespace backend.Services.Transportation;

public sealed class TricyclePointService(
    ITricyclePointRepository tricyclePointRepository,
    ITransportStopRepository transportStopRepository,
    IRoutingNetworkChangeNotifier? routingNetwork = null) : ITricyclePointService
{
    private const double EarthRadiusMeters = 6_371_000;

    private readonly ITricyclePointRepository _tricyclePointRepository = tricyclePointRepository;
    private readonly ITransportStopRepository _transportStopRepository = transportStopRepository;

    public Task<List<TricyclePoint>> GetAllActivePointsAsync(CancellationToken cancellationToken = default) =>
        _tricyclePointRepository.GetAllActiveAsync(cancellationToken);

    public Task<TricyclePoint?> GetPointByIdAsync(
        long tricyclePointId,
        CancellationToken cancellationToken = default)
    {
        if (tricyclePointId <= 0)
        {
            return Task.FromResult<TricyclePoint?>(null);
        }

        return _tricyclePointRepository.GetByIdAsync(tricyclePointId, cancellationToken);
    }

    public Task<TricyclePoint?> GetPointByCodeAsync(
        string pointCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedPointCode = NormalizeRequiredText(pointCode);
        if (normalizedPointCode is null)
        {
            return Task.FromResult<TricyclePoint?>(null);
        }

        return _tricyclePointRepository.GetByPointCodeAsync(normalizedPointCode, cancellationToken);
    }

    public bool IsLocationInsideTricyclePointRadius(
        TricyclePoint? tricyclePoint,
        double latitude,
        double longitude)
    {
        if (tricyclePoint is null ||
            tricyclePoint.RadiusMeters <= 0 ||
            !AreCoordinatesValid(latitude, longitude) ||
            !AreCoordinatesValid(tricyclePoint.CenterLatitude, tricyclePoint.CenterLongitude))
        {
            return false;
        }

        var distanceMeters = CalculateDistanceMeters(
            latitude,
            longitude,
            tricyclePoint.CenterLatitude,
            tricyclePoint.CenterLongitude);

        return distanceMeters <= tricyclePoint.RadiusMeters;
    }

    public async Task<List<TricyclePoint>> GetActivePointsCoveringLocationAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        if (!AreCoordinatesValid(latitude, longitude))
        {
            return [];
        }

        var activePoints = await _tricyclePointRepository.GetAllActiveAsync(cancellationToken);
        return activePoints
            .Where(point => IsLocationInsideTricyclePointRadius(point, latitude, longitude))
            .OrderBy(point => CalculateDistanceMeters(
                latitude,
                longitude,
                point.CenterLatitude,
                point.CenterLongitude))
            .ToList();
    }

    public async Task<TricyclePointMutationResult> AddVerifiedTricyclePointAsync(
        string pointCode,
        string pointName,
        double centerLatitude,
        double centerLongitude,
        int radiusMeters,
        long? stopId = null,
        string? description = null,
        string? address = null,
        string? operatorName = null,
        decimal? baseFare = null,
        decimal? farePerKilometer = null,
        int? averageWaitingTimeSeconds = null,
        TimeOnly? serviceStartTime = null,
        TimeOnly? serviceEndTime = null,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            tricyclePointId: null,
            pointCode,
            pointName,
            centerLatitude,
            centerLongitude,
            radiusMeters,
            stopId,
            baseFare,
            farePerKilometer,
            averageWaitingTimeSeconds,
            cancellationToken);
        if (validation.Errors.Count > 0)
        {
            return TricyclePointMutationResult.ValidationFailed(validation.Errors);
        }

        if (validation.DuplicateErrors.Count > 0)
        {
            return TricyclePointMutationResult.Duplicate(validation.DuplicateErrors);
        }

        var now = DateTime.UtcNow;
        var tricyclePoint = new TricyclePoint
        {
            StopId = stopId,
            PointCode = validation.PointCode!,
            PointName = validation.PointName!,
            Description = NormalizeOptionalText(description),
            Address = NormalizeOptionalText(address),
            OperatorName = NormalizeOptionalText(operatorName),
            CenterLatitude = centerLatitude,
            CenterLongitude = centerLongitude,
            RadiusMeters = radiusMeters,
            BaseFare = baseFare,
            FarePerKilometer = farePerKilometer,
            AverageWaitingTimeSeconds = averageWaitingTimeSeconds,
            ServiceStartTime = serviceStartTime,
            ServiceEndTime = serviceEndTime,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var createdPoint = await _tricyclePointRepository.AddAsync(tricyclePoint, cancellationToken);
        if (createdPoint.IsActive)
            routingNetwork?.Invalidate("active TODA point created");
        return TricyclePointMutationResult.Success(createdPoint);
    }

    public async Task<TricyclePointMutationResult> UpdateVerifiedTricyclePointAsync(
        long tricyclePointId,
        string pointCode,
        string pointName,
        double centerLatitude,
        double centerLongitude,
        int radiusMeters,
        long? stopId = null,
        string? description = null,
        string? address = null,
        string? operatorName = null,
        decimal? baseFare = null,
        decimal? farePerKilometer = null,
        int? averageWaitingTimeSeconds = null,
        TimeOnly? serviceStartTime = null,
        TimeOnly? serviceEndTime = null,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        if (tricyclePointId <= 0)
        {
            return TricyclePointMutationResult.NotFound(tricyclePointId);
        }

        var existingPoint = await _tricyclePointRepository.GetByIdAsync(tricyclePointId, cancellationToken);
        if (existingPoint is null)
        {
            return TricyclePointMutationResult.NotFound(tricyclePointId);
        }

        var wasActive = existingPoint.IsActive;

        var validation = await ValidateAsync(
            tricyclePointId,
            pointCode,
            pointName,
            centerLatitude,
            centerLongitude,
            radiusMeters,
            stopId,
            baseFare,
            farePerKilometer,
            averageWaitingTimeSeconds,
            cancellationToken);
        if (validation.Errors.Count > 0)
        {
            return TricyclePointMutationResult.ValidationFailed(validation.Errors);
        }

        if (validation.DuplicateErrors.Count > 0)
        {
            return TricyclePointMutationResult.Duplicate(validation.DuplicateErrors);
        }

        existingPoint.StopId = stopId;
        existingPoint.PointCode = validation.PointCode!;
        existingPoint.PointName = validation.PointName!;
        existingPoint.Description = NormalizeOptionalText(description);
        existingPoint.Address = NormalizeOptionalText(address);
        existingPoint.OperatorName = NormalizeOptionalText(operatorName);
        existingPoint.CenterLatitude = centerLatitude;
        existingPoint.CenterLongitude = centerLongitude;
        existingPoint.RadiusMeters = radiusMeters;
        existingPoint.BaseFare = baseFare;
        existingPoint.FarePerKilometer = farePerKilometer;
        existingPoint.AverageWaitingTimeSeconds = averageWaitingTimeSeconds;
        existingPoint.ServiceStartTime = serviceStartTime;
        existingPoint.ServiceEndTime = serviceEndTime;
        existingPoint.IsActive = isActive;
        existingPoint.UpdatedAt = DateTime.UtcNow;

        var updatedPoint = await _tricyclePointRepository.UpdateAsync(existingPoint, cancellationToken);
        if (wasActive || updatedPoint.IsActive)
            routingNetwork?.Invalidate("active TODA point updated or deactivated");
        return TricyclePointMutationResult.Success(updatedPoint);
    }

    private async Task<TricyclePointValidationResult> ValidateAsync(
        long? tricyclePointId,
        string pointCode,
        string pointName,
        double centerLatitude,
        double centerLongitude,
        int radiusMeters,
        long? stopId,
        decimal? baseFare,
        decimal? farePerKilometer,
        int? averageWaitingTimeSeconds,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var duplicateErrors = new List<string>();
        var normalizedPointCode = NormalizeRequiredText(pointCode);
        var normalizedPointName = NormalizeRequiredText(pointName);

        if (normalizedPointCode is null)
        {
            errors.Add("Point code is required.");
        }

        if (normalizedPointName is null)
        {
            errors.Add("Point name is required.");
        }

        AddCoordinateValidationErrors(centerLatitude, centerLongitude, errors);

        if (radiusMeters <= 0)
        {
            errors.Add("Radius meters must be greater than zero.");
        }

        if (baseFare < 0)
        {
            errors.Add("Base fare cannot be negative.");
        }

        if (farePerKilometer < 0)
        {
            errors.Add("Fare per kilometer cannot be negative.");
        }

        if (averageWaitingTimeSeconds < 0)
        {
            errors.Add("Average waiting time cannot be negative.");
        }

        if (stopId <= 0)
        {
            errors.Add("Transport stop id must be greater than zero when supplied.");
        }

        if (errors.Count > 0)
        {
            return new TricyclePointValidationResult(normalizedPointCode, normalizedPointName, errors, duplicateErrors);
        }

        if (normalizedPointCode is not null)
        {
            var existingByCode = await _tricyclePointRepository.GetByPointCodeAsync(
                normalizedPointCode,
                cancellationToken);
            if (IsDifferentPoint(existingByCode, tricyclePointId))
            {
                duplicateErrors.Add($"Point code {normalizedPointCode} is already used.");
            }
        }

        if (stopId.HasValue)
        {
            var stop = await _transportStopRepository.GetByIdAsync(stopId.Value, cancellationToken);
            if (stop is null)
            {
                errors.Add($"Transport stop {stopId.Value} was not found.");
            }

            var existingByStop = await _tricyclePointRepository.GetByStopIdAsync(stopId.Value, cancellationToken);
            if (IsDifferentPoint(existingByStop, tricyclePointId))
            {
                duplicateErrors.Add($"Transport stop {stopId.Value} is already assigned to another tricycle point.");
            }
        }

        return new TricyclePointValidationResult(normalizedPointCode, normalizedPointName, errors, duplicateErrors);
    }

    private static void AddCoordinateValidationErrors(
        double latitude,
        double longitude,
        List<string> errors)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            errors.Add("Coordinates must be finite numbers.");
            return;
        }

        if (latitude is < -90 or > 90)
        {
            errors.Add("Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            errors.Add("Longitude must be between -180 and 180.");
        }
    }

    private static bool AreCoordinatesValid(double latitude, double longitude) =>
        double.IsFinite(latitude) &&
        double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;

    private static bool IsDifferentPoint(TricyclePoint? tricyclePoint, long? existingPointId) =>
        tricyclePoint is not null &&
        tricyclePoint.TricyclePointId != existingPointId;

    private static string? NormalizeRequiredText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalText(string? value) => NormalizeRequiredText(value);

    private static double CalculateDistanceMeters(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        var lat1Rad = lat1 * Math.PI / 180;
        var lat2Rad = lat2 * Math.PI / 180;
        var deltaLat = (lat2 - lat1) * Math.PI / 180;
        var deltaLon = (lon2 - lon1) * Math.PI / 180;

        var a =
            Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
            Math.Cos(lat1Rad) *
            Math.Cos(lat2Rad) *
            Math.Sin(deltaLon / 2) *
            Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private sealed record TricyclePointValidationResult(
        string? PointCode,
        string? PointName,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> DuplicateErrors);
}
