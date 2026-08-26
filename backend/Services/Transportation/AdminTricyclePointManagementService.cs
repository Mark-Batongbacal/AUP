using backend.Models.Database;
using backend.Models.TricyclePointManagement;
using backend.Repositories;

namespace backend.Services.Transportation;

public sealed class AdminTricyclePointManagementService(
    ITricyclePointRepository tricyclePointRepository,
    ITricyclePointService tricyclePointService) : IAdminTricyclePointManagementService
{
    private const double EarthRadiusMeters = 6_371_000;
    private const double DefaultDuplicateThresholdMeters = 75;

    private readonly ITricyclePointRepository _tricyclePointRepository = tricyclePointRepository;
    private readonly ITricyclePointService _tricyclePointService = tricyclePointService;

    public async Task<IReadOnlyList<AdminTricyclePointResponse>> GetAllAsync(
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var points = includeArchived
            ? await _tricyclePointRepository.GetAllAsync(cancellationToken)
            : await _tricyclePointRepository.GetAllActiveAsync(cancellationToken);

        return points.Select(Map).ToList();
    }

    public async Task<AdminTricyclePointResponse?> GetByIdAsync(
        long tricyclePointId,
        CancellationToken cancellationToken = default)
    {
        if (tricyclePointId <= 0)
        {
            return null;
        }

        var point = await _tricyclePointRepository.GetByIdAsync(tricyclePointId, cancellationToken);
        return point is null ? null : Map(point);
    }

    public async Task<IReadOnlyList<TricyclePointDuplicateWarning>> GetDuplicateWarningsAsync(
        double latitude,
        double longitude,
        long? excludeTricyclePointId = null,
        double thresholdMeters = DefaultDuplicateThresholdMeters,
        CancellationToken cancellationToken = default)
    {
        if (!CoordinatesAreValid(latitude, longitude))
        {
            return [];
        }

        thresholdMeters = double.IsFinite(thresholdMeters) && thresholdMeters > 0
            ? Math.Min(thresholdMeters, 5_000)
            : DefaultDuplicateThresholdMeters;

        var points = await _tricyclePointRepository.GetAllAsync(cancellationToken);
        return points
            .Where(point => point.TricyclePointId != excludeTricyclePointId)
            .Select(point => new
            {
                Point = point,
                Distance = CalculateDistanceMeters(
                    latitude,
                    longitude,
                    point.CenterLatitude,
                    point.CenterLongitude)
            })
            .Where(item => item.Distance <= thresholdMeters)
            .OrderBy(item => item.Distance)
            .Select(item => new TricyclePointDuplicateWarning(
                item.Point.TricyclePointId,
                item.Point.PointCode,
                item.Point.PointName,
                Math.Round(item.Distance, 1),
                item.Point.IsActive))
            .ToList();
    }

    public async Task<AdminTricyclePointMutationResult> CreateAsync(
        AdminTricyclePointMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Latitude is null || request.Longitude is null ||
            !CoordinatesAreValid(request.Latitude.Value, request.Longitude.Value))
        {
            return AdminTricyclePointMutationResult.Invalid(
                ["Valid latitude and longitude are required."]);
        }

        var latitude = request.Latitude.Value;
        var longitude = request.Longitude.Value;
        var warnings = await GetDuplicateWarningsAsync(
            latitude,
            longitude,
            cancellationToken: cancellationToken);

        var result = await _tricyclePointService.AddVerifiedTricyclePointAsync(
            request.PointCode,
            request.PointName,
            latitude,
            longitude,
            request.RadiusMeters,
            request.StopId,
            request.Description,
            request.Address,
            request.OperatorName,
            request.BaseFare,
            request.FarePerKilometer,
            request.AverageWaitingTimeSeconds,
            request.ServiceStartTime,
            request.ServiceEndTime,
            request.IsActive,
            cancellationToken);

        return MapMutation(result, warnings);
    }

    public async Task<AdminTricyclePointMutationResult> UpdateAsync(
        long tricyclePointId,
        AdminTricyclePointMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tricyclePointId <= 0)
        {
            return AdminTricyclePointMutationResult.Missing();
        }

        if (request.Latitude is null || request.Longitude is null ||
            !CoordinatesAreValid(request.Latitude.Value, request.Longitude.Value))
        {
            return AdminTricyclePointMutationResult.Invalid(
                ["Valid latitude and longitude are required."]);
        }

        var latitude = request.Latitude.Value;
        var longitude = request.Longitude.Value;
        var warnings = await GetDuplicateWarningsAsync(
            latitude,
            longitude,
            tricyclePointId,
            cancellationToken: cancellationToken);

        var result = await _tricyclePointService.UpdateVerifiedTricyclePointAsync(
            tricyclePointId,
            request.PointCode,
            request.PointName,
            latitude,
            longitude,
            request.RadiusMeters,
            request.StopId,
            request.Description,
            request.Address,
            request.OperatorName,
            request.BaseFare,
            request.FarePerKilometer,
            request.AverageWaitingTimeSeconds,
            request.ServiceStartTime,
            request.ServiceEndTime,
            request.IsActive,
            cancellationToken);

        return MapMutation(result, warnings);
    }

    public async Task<AdminTricyclePointMutationResult> SetActiveAsync(
        long tricyclePointId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (tricyclePointId <= 0)
        {
            return AdminTricyclePointMutationResult.Missing();
        }

        var point = await _tricyclePointRepository.GetByIdAsync(tricyclePointId, cancellationToken);
        if (point is null)
        {
            return AdminTricyclePointMutationResult.Missing();
        }

        point.IsActive = isActive;
        point.UpdatedAt = DateTime.UtcNow;
        var updated = await _tricyclePointRepository.UpdateAsync(point, cancellationToken);
        return AdminTricyclePointMutationResult.Success(
            new AdminTricyclePointMutationResponse(Map(updated), []));
    }

    private static AdminTricyclePointMutationResult MapMutation(
        TricyclePointMutationResult result,
        IReadOnlyList<TricyclePointDuplicateWarning> warnings)
    {
        if (result.Status == TricyclePointMutationStatus.Success && result.TricyclePoint is not null)
        {
            return AdminTricyclePointMutationResult.Success(
                new AdminTricyclePointMutationResponse(Map(result.TricyclePoint), warnings));
        }

        return result.Status switch
        {
            TricyclePointMutationStatus.NotFound => AdminTricyclePointMutationResult.Missing(),
            TricyclePointMutationStatus.Duplicate => AdminTricyclePointMutationResult.StateConflict(result.Errors),
            _ => AdminTricyclePointMutationResult.Invalid(result.Errors)
        };
    }

    private static AdminTricyclePointResponse Map(TricyclePoint point) => new(
        point.TricyclePointId,
        point.StopId,
        point.PointCode,
        point.PointName,
        point.Description,
        point.Address,
        point.OperatorName,
        point.CenterLatitude,
        point.CenterLongitude,
        point.RadiusMeters,
        point.BaseFare,
        point.FarePerKilometer,
        point.AverageWaitingTimeSeconds,
        point.ServiceStartTime,
        point.ServiceEndTime,
        point.IsActive,
        point.CreatedAt,
        point.UpdatedAt);

    private static bool CoordinatesAreValid(double latitude, double longitude) =>
        double.IsFinite(latitude) &&
        double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;

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
            Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
            Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }
}
