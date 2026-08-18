using backend.Models.Database;
using backend.Repositories;
using Microsoft.Extensions.Options;

namespace backend.Services.Navigation;

public interface ILandmarkService
{
    Task<IReadOnlyList<NavigationInstruction>> EvaluateAsync(TripSession session, RecommendationLeg leg, double previousProgress, double currentProgress, CancellationToken cancellationToken = default);
}

public sealed class LandmarkService(
    IOptions<NavigationOptions> options,
    ITripLandmarkCandidateRepository tripCache) : ILandmarkService
{
    private readonly NavigationOptions _options = options.Value;

    public async Task<IReadOnlyList<NavigationInstruction>> EvaluateAsync(
        TripSession session, RecommendationLeg leg, double previousProgress,
        double currentProgress, CancellationToken cancellationToken = default)
    {
        if (leg.RouteId is null || currentProgress <= previousProgress) return [];
        var selected = await tripCache.GetCrossedAsync(
            session.TripSessionId, leg.LegOrder, previousProgress, currentProgress, cancellationToken);
        var result = new List<NavigationInstruction>();
        foreach (var item in selected.Take(_options.MaximumLandmarksPerLeg))
        {
            await tripCache.MarkTriggeredAsync(
                item.TripLandmarkCandidateId, DateTime.UtcNow, cancellationToken);
            result.Add(new NavigationInstruction
            {
                TripSessionId = session.TripSessionId, LegIndex = leg.LegOrder,
                Type = NavigationInstructionType.LandmarkNotice,
                Text = $"You just passed {item.Name}.",
                Latitude = item.Latitude, Longitude = item.Longitude,
                DistanceFromRouteStartMeters = item.DistanceFromRouteStartMeters
            });
        }
        return result;
    }
}
