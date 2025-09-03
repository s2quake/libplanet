using System.Reactive.Subjects;

namespace Libplanet.TestUtilities;

public sealed class ObservableEmissionCounter<T> : IDisposable
{
    private readonly Subject<int> _countChangedSubject = new();
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
                _countChangedSubject.OnNext(_count);
            }
        });
    }

    public int Count => Interlocked.CompareExchange(ref _count, 0, 0);

    public IObservable<int> CountChanged => _countChangedSubject;

    public void Dispose()
    {
        if (!_disposed)
        {
            _countChangedSubject.Dispose();
            _subscription.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
