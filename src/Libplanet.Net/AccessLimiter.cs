using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal sealed class AccessLimiter(int maxCount) : IDisposable
{
    private readonly SemaphoreSlim? _semaphore = maxCount > 0 ? new SemaphoreSlim(maxCount, maxCount) : null;

    public void Dispose() => _semaphore?.Dispose();

    public async Task<IDisposable?> WaitAsync(CancellationToken cancellationToken)
    {
        if (_semaphore is { } sema)
        {
            await sema.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            return new Releaser(sema);
        }

        return new Releaser(_semaphore);
    }

    // public int Release()
    // {
    //     if (_semaphore is { } sema)
    //     {
    //         return sema.Release();
    //     }

    //     return -1;
    // }

    private sealed class Releaser(SemaphoreSlim? semaphore) : IDisposable
    {
        public void Dispose() => semaphore?.Release();
    }
}
