namespace Libplanet.Types.Threading;

public readonly struct ReadScope : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;

    public ReadScope(ReaderWriterLockSlim @lock)
    {
        _lock = @lock;
        _lock.EnterReadLock();
    }

    public void Dispose() => _lock.ExitReadLock();
}
