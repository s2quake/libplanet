namespace Libplanet.Net;

internal sealed class AccessLimiter(int maximum) : IDisposable
{
    private readonly SemaphoreSlim? _semaphore = maximum > 0 ? new SemaphoreSlim(maximum, maximum) : null;

    public void Dispose() => _semaphore?.Dispose();

    public async Task<IDisposable?> CanAccessAsync(CancellationToken cancellationToken)
    {
        if (_semaphore is { } semaphore)
        {
            if (await semaphore.WaitAsync(TimeSpan.Zero, cancellationToken))
            {
                return new Releaser(semaphore);
            }

            return null;
        }

        return new Releaser(null);
    }

    private sealed class Releaser(SemaphoreSlim? semaphore) : IDisposable
    {
        public void Dispose() => semaphore?.Release();
    }
}
