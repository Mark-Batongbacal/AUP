using backend.Models.Database;
using backend.Repositories;
using backend.Services.Routing;
using Microsoft.Extensions.Options;

namespace backend.Services.Navigation;

public interface INavigationInstructionService
{
    Task<IReadOnlyList<NavigationInstruction>> GenerateAsync(TripSession session, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NavigationInstruction>> GetOwnedAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class NavigationInstructionService(
    IRouteRecommendationRepository recommendations,
    INavigationInstructionRepository instructionRepository,
    IValhallaService valhalla,
    IOptions<NavigationOptions> options,
    ILogger<NavigationInstructionService> logger) : INavigationInstructionService
{
    private readonly NavigationOptions _options = options.Value;
    public async Task<IReadOnlyList<NavigationInstruction>> GenerateAsync(
        TripSession session, CancellationToken cancellationToken = default)
    {
        var legs = await recommendations.GetOrderedLegsAsync(session.RecommendationId, cancellationToken);
        var output = new List<NavigationInstruction>();
        foreach (var leg in legs.OrderBy(item => item.LegOrder))
        {
            var code = leg.TransportMode?.Code?.ToUpperInvariant();
            if (code is "WALK" or "WALKING" or "PEDESTRIAN")
                await AddWalkingAsync(output, session, leg, cancellationToken);
            else if (code == "JEEPNEY")
                AddJeepney(output, session, leg, legs);
            else if (code is "TRICYCLE" or "TRIKE")
                await AddTricycleAsync(output, session, leg, cancellationToken);
            else
                Add(output, session, leg, NavigationInstructionType.Continue,
                    $"Continue toward {leg.ToName ?? "the next stop"}.");
        }

        Add(output, session, new RecommendationLeg { LegOrder = Math.Max(0, legs.Count - 1) },
            NavigationInstructionType.Arrived, "You have arrived.", false,
            session.DestinationLatitude, session.DestinationLongitude);
        await instructionRepository.ReplaceForSessionAsync(
            session.TripSessionId, output, cancellationToken);
        return output;
    }

    public async Task<IReadOnlyList<NavigationInstruction>> GetOwnedAsync(
        Guid sessionId, Guid userId, CancellationToken cancellationToken = default) =>
        await instructionRepository.GetForOwnedSessionAsync(sessionId, userId, cancellationToken);

    private async Task AddWalkingAsync(List<NavigationInstruction> output, TripSession session,
        RecommendationLeg leg, CancellationToken cancellationToken)
    {
        if (leg.StartLatitude is null || leg.StartLongitude is null ||
            leg.EndLatitude is null || leg.EndLongitude is null)
        {
            Add(output, session, leg, NavigationInstructionType.Continue,
                $"Walk toward {leg.ToName ?? "the next point"}.");
            return;
        }

        try
        {
            var route = await valhalla.GetRouteAsync(
                leg.StartLatitude.Value, leg.StartLongitude.Value,
                leg.EndLatitude.Value, leg.EndLongitude.Value,
                "pedestrian", cancellationToken);
            var distance = 0d;
            foreach (var maneuver in route.Trip?.Legs.SelectMany(item => item.Maneuvers) ?? [])
            {
                var point = route.Trip?.Legs.FirstOrDefault()?.Points.ElementAtOrDefault(maneuver.BeginShapeIndex);
                Add(output, session, leg, MapManeuver(maneuver.Type),
                    string.IsNullOrWhiteSpace(maneuver.Instruction) ? "Continue walking." : maneuver.Instruction,
                    false, point?.ElementAtOrDefault(1), point?.ElementAtOrDefault(0), distance,
                    maneuver);
                distance += maneuver.Length * 1_000;
            }
            if (!output.Any(item => item.LegIndex == leg.LegOrder))
                Add(output, session, leg, NavigationInstructionType.Continue,
                    $"Walk toward {leg.ToName ?? "the next point"}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Walking instructions unavailable for trip session {TripSessionId}, leg {LegIndex}", session.TripSessionId, leg.LegOrder);
            Add(output, session, leg, NavigationInstructionType.Continue,
                $"Walk toward {leg.ToName ?? "the next point"}.");
        }
    }

    private static void AddJeepney(List<NavigationInstruction> output, TripSession session,
        RecommendationLeg leg, IReadOnlyList<RecommendationLeg> legs)
    {
        var route = leg.Route?.RouteName ?? leg.Instructions ?? "the selected jeepney";
        Add(output, session, leg, NavigationInstructionType.BoardJeepney, $"Board {route}.", true, leg.StartLatitude, leg.StartLongitude);
        Add(output, session, leg, NavigationInstructionType.Continue, $"Stay on {route}.");
        Add(output, session, leg, NavigationInstructionType.PrepareToAlight, "Prepare to get off.", false, leg.EndLatitude, leg.EndLongitude);
        Add(output, session, leg, NavigationInstructionType.AlightJeepney, "Get off here.", true, leg.EndLatitude, leg.EndLongitude);
        if (legs.Any(item => item.LegOrder > leg.LegOrder && item.TransportMode?.Code == "JEEPNEY"))
            Add(output, session, leg, NavigationInstructionType.Transfer, "Transfer to the next jeepney.");
    }

    private async Task AddTricycleAsync(List<NavigationInstruction> output, TripSession session,
        RecommendationLeg leg, CancellationToken cancellationToken)
    {
        Add(output, session, leg, NavigationInstructionType.BoardTricycle,
            $"Board at {leg.FromName ?? "the tricycle pickup point"}.", true, leg.StartLatitude, leg.StartLongitude);
        if (leg.StartLatitude is { } startLat && leg.StartLongitude is { } startLon &&
            leg.EndLatitude is { } endLat && leg.EndLongitude is { } endLon)
        {
            try
            {
                var route = await valhalla.GetRouteAsync(startLat, startLon, endLat, endLon,
                    _options.TricycleRoadCosting, cancellationToken);
                var distance = 0d;
                foreach (var routeLeg in route.Trip?.Legs ?? [])
                {
                    foreach (var maneuver in routeLeg.Maneuvers)
                    {
                        var point = routeLeg.Points.ElementAtOrDefault(maneuver.BeginShapeIndex);
                        Add(output, session, leg, MapManeuver(maneuver.Type),
                            string.IsNullOrWhiteSpace(maneuver.Instruction)
                                ? "Continue along the road." : maneuver.Instruction,
                            false, point?.ElementAtOrDefault(1), point?.ElementAtOrDefault(0),
                            distance, maneuver, NavigationInstructionAudience.Driver);
                        distance += maneuver.Length * 1_000;
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception,
                    "Tricycle road guidance unavailable for trip session {TripSessionId}, leg {LegIndex}",
                    session.TripSessionId, leg.LegOrder);
            }
        }
        if (!output.Any(item => item.LegIndex == leg.LegOrder &&
                               item.Audience == NavigationInstructionAudience.Driver))
            Add(output, session, leg, NavigationInstructionType.Continue,
                $"Drive toward {leg.ToName ?? "the next point"}.",
                audience: NavigationInstructionAudience.Driver);
        Add(output, session, leg, NavigationInstructionType.PrepareToAlight, "Prepare to get off.", false, leg.EndLatitude, leg.EndLongitude);
        Add(output, session, leg, NavigationInstructionType.AlightTricycle, "Get off here.", true, leg.EndLatitude, leg.EndLongitude);
    }

    private static NavigationInstructionType MapManeuver(int type) => type switch
    {
        15 or 16 or 17 or 18 => NavigationInstructionType.TurnRight,
        8 or 9 or 10 or 11 => NavigationInstructionType.TurnLeft,
        26 or 27 => NavigationInstructionType.Roundabout,
        4 or 5 => NavigationInstructionType.Arrived,
        _ => NavigationInstructionType.Continue
    };

    private static void Add(List<NavigationInstruction> output, TripSession session,
        RecommendationLeg leg, NavigationInstructionType type, string text,
        bool requiresConfirmation = false, double? latitude = null, double? longitude = null,
        double? distance = null, backend.Models.Valhalla.ValhallaManeuver? maneuver = null,
        NavigationInstructionAudience audience = NavigationInstructionAudience.Passenger) =>
        output.Add(new NavigationInstruction
        {
            TripSessionId = session.TripSessionId, Sequence = output.Count,
            LegIndex = leg.LegOrder, Type = type, Text = text,
            Audience = audience,
            Latitude = latitude, Longitude = longitude,
            DistanceFromLegStartMeters = distance,
            TriggerDistanceMeters = requiresConfirmation ? 0 : 30,
            RequiresConfirmation = requiresConfirmation,
            StreetName = maneuver?.StreetNames.FirstOrDefault(),
            SourceManeuverType = maneuver?.Type,
            BeginShapeIndex = maneuver?.BeginShapeIndex,
            EndShapeIndex = maneuver?.EndShapeIndex
        });
}
