using System.Collections.Concurrent;
using System.Text.Json;
using backend.Models.Routing;
using backend.Services.Routing;
using backend.Services.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class RoutingAdmissionControlTests
{
    [Fact]
    public async Task AcquireAsync_AdmitsQueuedRequestsInFifoOrder()
    {
        var controller = CreateController(maxConcurrent: 1, maxQueue: 2);
        using var first = await controller.AcquireAsync();

        var secondTask = controller.AcquireAsync().AsTask();
        var thirdTask = controller.AcquireAsync().AsTask();

        Assert.False(secondTask.IsCompleted);
        Assert.False(thirdTask.IsCompleted);

        first.Dispose();
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(thirdTask.IsCompleted);

        second.Dispose();
        using var third = await thirdTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AcquireAsync_WhenQueueIsFull_RejectsImmediately()
    {
        var telemetry = new RecordingTelemetry();
        var controller = CreateController(
            maxConcurrent: 1,
            maxQueue: 1,
            telemetry: telemetry);
        using var first = await controller.AcquireAsync();
        var queued = controller.AcquireAsync().AsTask();

        var exception = await Assert.ThrowsAsync<RoutingAdmissionRejectedException>(
            () => controller.AcquireAsync().AsTask());

        Assert.Equal(
            RoutingAdmissionRejectionReason.QueueFull,
            exception.Reason);
        Assert.Equal(1, telemetry.Counts["routing_admission_rejected"]);

        first.Dispose();
        using var admitted = await queued;
    }

    [Fact]
    public async Task AcquireAsync_WhenQueueWaitExpires_RemovesWaiterAndReturnsRetryableRejection()
    {
        var telemetry = new RecordingTelemetry();
        var controller = CreateController(
            maxConcurrent: 1,
            maxQueue: 1,
            maxQueueWaitSeconds: 1,
            telemetry: telemetry);
        using var first = await controller.AcquireAsync();

        var exception = await Assert.ThrowsAsync<RoutingAdmissionRejectedException>(
            () => controller.AcquireAsync().AsTask());

        Assert.Equal(
            RoutingAdmissionRejectionReason.QueueTimeout,
            exception.Reason);
        Assert.Equal(1, telemetry.Counts["routing_admission_timed_out"]);
        Assert.Equal(0, telemetry.Values["routing_admission_queue_depth"]);
    }

    [Fact]
    public async Task AcquireAsync_WhenCanceled_RemovesWaiterWithoutLeakingCapacity()
    {
        var telemetry = new RecordingTelemetry();
        var controller = CreateController(
            maxConcurrent: 1,
            maxQueue: 1,
            telemetry: telemetry);
        using var first = await controller.AcquireAsync();
        using var cancellation = new CancellationTokenSource();
        var canceledTask = controller.AcquireAsync(cancellation.Token).AsTask();

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledTask);
        Assert.Equal(1, telemetry.Counts["routing_admission_canceled"]);

        first.Dispose();
        using var replacement = await controller.AcquireAsync().AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Decorator_PreservesPlanOrderPreferencesAndGeometryForwarding()
    {
        var preferences = new JourneyPlanningPreferences();
        var plans = new List<JeepneyTripPlan>
        {
            new()
            {
                RecommendationType = "fastest",
                OriginAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
                DestinationAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk }
            },
            new()
            {
                RecommendationType = "cheapest",
                OriginAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk },
                DestinationAccess = new JeepneyAccessSegment { Mode = AccessMode.Walk }
            }
        };
        var inner = new Mock<IRoutingPlanningPipeline>(MockBehavior.Strict);
        inner.Setup(service => service.PlanTripsAsync(
                15, 120, 15.1, 120.1, preferences, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);
        inner.Setup(service => service.EnrichSelectedPlanGeometryAsync(
                plans, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var lease = new Mock<IDisposable>(MockBehavior.Strict);
        lease.Setup(item => item.Dispose());
        var admission = new Mock<IRoutingAdmissionController>(MockBehavior.Strict);
        admission.Setup(item => item.AcquireAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IDisposable>(lease.Object));
        var service = new RoutingAdmissionControlledService(
            inner.Object,
            admission.Object,
            NullTukiTelemetry.Instance);

        var actual = await service.PlanTripsAsync(
            15, 120, 15.1, 120.1, preferences);
        await service.EnrichSelectedPlanGeometryAsync(actual);

        Assert.Same(plans, actual);
        Assert.Same(plans[0], actual[0]);
        Assert.Same(plans[1], actual[1]);
        admission.Verify(item => item.AcquireAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        lease.Verify(item => item.Dispose(), Times.Once);
        inner.VerifyAll();
    }

    [Fact]
    public async Task Decorator_DoesNotGateNearbyRouteDiscovery()
    {
        var nearby = new List<NearbyJeepneyResponse>();
        var inner = new Mock<IRoutingPlanningPipeline>(MockBehavior.Strict);
        inner.Setup(service => service.FindNearbyRoutesAsync(
                15, 120, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nearby);
        var admission = new Mock<IRoutingAdmissionController>(MockBehavior.Strict);
        var service = new RoutingAdmissionControlledService(
            inner.Object,
            admission.Object,
            NullTukiTelemetry.Instance);

        var actual = await service.FindNearbyRoutesAsync(15, 120);

        Assert.Same(nearby, actual);
        inner.VerifyAll();
        admission.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Middleware_MapsAdmissionRejectionTo429WithRetryAfter()
    {
        var middleware = new RoutingAdmissionExceptionMiddleware(
            _ => Task.FromException(new RoutingAdmissionRejectedException(
                RoutingAdmissionRejectionReason.QueueFull,
                retryAfterSeconds: 7)));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("7", context.Response.Headers.RetryAfter);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(
            "ROUTING_BUSY",
            body.RootElement.GetProperty("error").GetString());
        Assert.Equal(
            7,
            body.RootElement.GetProperty("retryAfterSeconds").GetInt32());
    }

    [Fact]
    public void Options_RejectInvalidLimits()
    {
        Assert.False(new RoutingAdmissionOptions
        {
            MaxConcurrentPlans = 0
        }.IsValid());
        Assert.False(new RoutingAdmissionOptions
        {
            MaxQueueLength = -1
        }.IsValid());
        Assert.False(new RoutingAdmissionOptions
        {
            MaxQueueWaitSeconds = 0
        }.IsValid());
        Assert.True(new RoutingAdmissionOptions().IsValid());
    }

    private static RoutingAdmissionController CreateController(
        int maxConcurrent,
        int maxQueue,
        int maxQueueWaitSeconds = 30,
        RecordingTelemetry? telemetry = null) =>
        new(
            Options.Create(new RoutingAdmissionOptions
            {
                MaxConcurrentPlans = maxConcurrent,
                MaxQueueLength = maxQueue,
                MaxQueueWaitSeconds = maxQueueWaitSeconds,
                RetryAfterSeconds = 5
            }),
            telemetry ?? new RecordingTelemetry(),
            NullLogger<RoutingAdmissionController>.Instance);

    private sealed class RecordingTelemetry : ITukiTelemetry
    {
        public ConcurrentDictionary<string, long> Counts { get; } = new();
        public ConcurrentDictionary<string, double> Values { get; } = new();
        public ConcurrentDictionary<string, List<double>> Observations { get; } = new();

        public void IncrementRouting(string metricName, long value = 1) =>
            Counts.AddOrUpdate(metricName, value, (_, current) => current + value);
        public void SetRoutingValue(string metricName, double value) =>
            Values[metricName] = value;
        public void ObserveRouting(string metricName, double value) =>
            Observations.GetOrAdd(metricName, _ => []).Add(value);
        public void Event(string eventName, Guid? tripSessionId = null, string? outcome = null) { }
        public IDisposable Measure(string operationName) => Empty.Instance;
        public void RecordRequest(string path, int statusCode, double elapsedMilliseconds) { }
        public IRoutingTelemetryScope BeginRoutingPlan(
            string source,
            CancellationToken cancellationToken = default) => EmptyScope.Instance;
        public IRoutingTelemetryScope BeginRoutingPass(
            int maxTransfers,
            CancellationToken cancellationToken = default) => EmptyScope.Instance;
        public IDisposable BeginRoutingStage(string stageName) => Empty.Instance;
        public IDisposable MeasureRouting(string operationName) => Empty.Instance;
        public void RecordRoutingAccessDiscoveryRoute(
            string routeId,
            int routeSampleCount,
            double boardDiscoveryMilliseconds,
            double directConnectionDiscoveryMilliseconds,
            double prefixComputationMilliseconds,
            double destinationAccessMilliseconds,
            long todaCandidatesConsidered,
            long todaCandidatesSurvivingFilters,
            long todaCandidatesSelected,
            long boardAccessAlternatives,
            long destinationAccessAlternatives,
            long directConnections) { }

        private sealed class Empty : IDisposable
        {
            public static Empty Instance { get; } = new();
            public void Dispose() { }
        }

        private sealed class EmptyScope : IRoutingTelemetryScope
        {
            public static EmptyScope Instance { get; } = new();
            public void Complete(string outcome) { }
            public void Dispose() { }
        }
    }
}
