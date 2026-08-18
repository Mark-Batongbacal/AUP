using backend.Models.Database;
using backend.Models.Valhalla;
using backend.Repositories;
using backend.Services.Navigation;
using backend.Services.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace backend.Tests.Services.Navigation;

public sealed class NavigationInstructionServiceTests
{
    private readonly Mock<IRouteRecommendationRepository> _recommendations = new();
    private readonly Mock<INavigationInstructionRepository> _instructions = new();
    private readonly Mock<IValhallaService> _valhalla = new();

    [Fact]
    public async Task WalkingManeuvers_AreMappedAndUnknownTypesContinueSafely()
    {
        SetupLegs(WalkLeg());
        _valhalla.Setup(service => service.GetRouteAsync(
                15.1, 120.5, 15.2, 120.6, "pedestrian", default))
            .ReturnsAsync(new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
                    Legs = [new ValhallaLeg
                    {
                        Points = [[120.5, 15.1], [120.6, 15.2]],
                        Maneuvers =
                        [
                            new() { Type = 10, Instruction = "Turn left", BeginShapeIndex = 0, Length = .1 },
                            new() { Type = 999, Instruction = "Use the footbridge", BeginShapeIndex = 1, Length = .2 }
                        ]
                    }]
                }
            });

        var result = await Service().GenerateAsync(Session());
        Assert.Contains(result, item => item.Type == NavigationInstructionType.TurnLeft);
        Assert.Contains(result, item => item.Type == NavigationInstructionType.Continue && item.SourceManeuverType == 999);
    }

    [Fact]
    public async Task JeepneyInstructions_ComeFromPersistedTukiLeg()
    {
        SetupLegs(new RecommendationLeg
        {
            LegOrder = 0,
            TransportMode = new TransportMode { Code = "JEEPNEY" },
            Route = new TransportRoute { RouteName = "Porac-Angeles" },
            StartLatitude = 15.1, StartLongitude = 120.5,
            EndLatitude = 15.2, EndLongitude = 120.6
        });
        var result = await Service().GenerateAsync(Session());
        Assert.Contains(result, item => item.Type == NavigationInstructionType.BoardJeepney && item.Text.Contains("Porac-Angeles"));
        Assert.Contains(result, item => item.Type == NavigationInstructionType.PrepareToAlight);
        Assert.Contains(result, item => item.Type == NavigationInstructionType.AlightJeepney && item.RequiresConfirmation);
        _valhalla.Verify(service => service.GetRouteAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WalkingProviderFailure_DegradesToDeterministicInstruction()
    {
        SetupLegs(WalkLeg());
        _valhalla.Setup(service => service.GetRouteAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                "pedestrian", default))
            .ThrowsAsync(new HttpRequestException());
        var result = await Service().GenerateAsync(Session());
        Assert.Contains(result, item => item.Type == NavigationInstructionType.Continue && item.Text.Contains("Walk"));
        Assert.Contains(result, item => item.Type == NavigationInstructionType.Arrived);
    }

    [Fact]
    public async Task TricycleLeg_GeneratesDriverFacingRoadGuidance()
    {
        SetupLegs(new RecommendationLeg
        {
            LegOrder = 0, TransportMode = new TransportMode { Code = "TRICYCLE" },
            FromName = "TODA terminal", ToName = "destination",
            StartLatitude = 15.1, StartLongitude = 120.5,
            EndLatitude = 15.2, EndLongitude = 120.6
        });
        _valhalla.Setup(service => service.GetRouteAsync(
                15.1, 120.5, 15.2, 120.6, "auto", default))
            .ReturnsAsync(new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
                    Legs = [new ValhallaLeg
                    {
                        Points = [[120.5, 15.1]],
                        Maneuvers = [new() { Type = 15, Instruction = "Turn right", Length = .2 }]
                    }]
                }
            });

        var result = await Service().GenerateAsync(Session());

        Assert.Contains(result, item => item.Type == NavigationInstructionType.BoardTricycle &&
                                        item.Audience == NavigationInstructionAudience.Passenger);
        Assert.Contains(result, item => item.Type == NavigationInstructionType.TurnRight &&
                                        item.Audience == NavigationInstructionAudience.Driver);
        Assert.Contains(result, item => item.Type == NavigationInstructionType.AlightTricycle);
    }

    [Fact]
    public async Task TricycleRoadFailure_PreservesFallbackDriverGuidance()
    {
        SetupLegs(new RecommendationLeg
        {
            LegOrder = 0, TransportMode = new TransportMode { Code = "TRIKE" },
            StartLatitude = 15.1, StartLongitude = 120.5,
            EndLatitude = 15.2, EndLongitude = 120.6, ToName = "hospital"
        });
        _valhalla.Setup(service => service.GetRouteAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                "auto", default)).ThrowsAsync(new HttpRequestException());

        var result = await Service().GenerateAsync(Session());

        Assert.Contains(result, item => item.Audience == NavigationInstructionAudience.Driver &&
                                        item.Text.Contains("hospital"));
    }

    private NavigationInstructionService Service() => new(
        _recommendations.Object, _instructions.Object, _valhalla.Object,
        Microsoft.Extensions.Options.Options.Create(new NavigationOptions()),
        NullLogger<NavigationInstructionService>.Instance);

    private void SetupLegs(params RecommendationLeg[] legs) =>
        _recommendations.Setup(repository => repository.GetOrderedLegsAsync(
            It.IsAny<Guid>(), default)).ReturnsAsync(legs.ToList());

    private static RecommendationLeg WalkLeg() => new()
    {
        LegOrder = 0, TransportMode = new TransportMode { Code = "WALK" },
        StartLatitude = 15.1, StartLongitude = 120.5,
        EndLatitude = 15.2, EndLongitude = 120.6, ToName = "pickup"
    };

    private static TripSession Session() => new()
    {
        TripSessionId = Guid.NewGuid(), RecommendationId = Guid.NewGuid(),
        DestinationLatitude = 15.2, DestinationLongitude = 120.6
    };
}
