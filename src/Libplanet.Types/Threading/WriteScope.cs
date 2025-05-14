using System.Threading;

namespace Libplanet.Types.Threading;

public readonly struct WriteScope : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;

    public WriteScope(ReaderWriterLockSlim rwLock)
    {
        _lock = rwLock;
        _lock.EnterWriteLock();
    }

    public void Dispose() => _lock.ExitWriteLock();
}
