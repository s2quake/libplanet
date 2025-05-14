using System.Threading;

namespace Libplanet.Types.Threading;

public readonly struct ReadScope : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;

    public ReadScope(ReaderWriterLockSlim rwLock)
    {
        _lock = rwLock;
        _lock.EnterReadLock();
    }

    public void Dispose() => _lock.ExitReadLock();
}
