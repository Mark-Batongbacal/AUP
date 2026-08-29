namespace backend.Services.Routing;

public interface IValhallaConcurrencyGate
{
    int MaxConcurrency { get; }
    ValueTask<IDisposable> AcquireAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One process-wide permit pool shared by every typed Valhalla client.
/// </summary>
public sealed class ValhallaConcurrencyGate : IValhallaConcurrencyGate, IDisposable
{
    private const int DefaultMaxConcurrentRequests = 5;
    private readonly SemaphoreSlim _semaphore;

    public ValhallaConcurrencyGate(IConfiguration configuration)
    {
        var configured = configuration.GetValue<int?>(
            "Valhalla:MaxConcurrentRequests");
        MaxConcurrency = configured is > 0
            ? configured.Value
            : DefaultMaxConcurrentRequests;
        _semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    public int MaxConcurrency { get; }

    public async ValueTask<IDisposable> AcquireAsync(
        CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                semaphore.Release();
        }
    }
}
