namespace backend.Services.Transportation;

public interface IRouteGeneratorService
{
    Task<IReadOnlyList<List<double>>> GenerateAsync(
        IReadOnlyList<List<double>> waypoints,
        CancellationToken cancellationToken = default);
}
