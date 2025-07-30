using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal sealed class NullableSemaphore(int maxCount) : IDisposable
{
    private readonly SemaphoreSlim? _sema = maxCount > 0
        ? new SemaphoreSlim(maxCount, maxCount)
        : null;

    public void Dispose()
    {
        _sema?.Dispose();
    }

    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_sema is { } sema)
        {
            return await sema.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public int Release()
    {
        if (_sema is { } sema)
        {
            return sema.Release();
        }

        return -1;
    }
}
