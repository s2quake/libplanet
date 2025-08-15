namespace Libplanet.TestUtilities;

public sealed class ObservableEmissionCounter<T> : IDisposable
{
    private readonly IDisposable _subscription;
    private int _count;
    private bool _disposed;

    public ObservableEmissionCounter(IObservable<T> observable)
        : this(observable, _ => true)
    {
    }

    public ObservableEmissionCounter(IObservable<T> observable, Func<T, bool> predicate)
    {
        _subscription = observable.Subscribe(item =>
        {
            if (predicate(item))
            {
                Interlocked.Increment(ref _count);
            }
        });
    }

    public int Count => Interlocked.CompareExchange(ref _count, 0, 0);

    public void Dispose()
    {
        if (!_disposed)
        {
            _subscription.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
