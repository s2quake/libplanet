using System.Threading;

namespace Libplanet.Types.Threading;

public readonly struct WriteScope : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;

    public WriteScope(ReaderWriterLockSlim @lock)
    {
        _lock = @lock;
        _lock.EnterWriteLock();
    }

    public void Dispose() => _lock.ExitWriteLock();
}
