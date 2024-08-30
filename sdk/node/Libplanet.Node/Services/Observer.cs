namespace Libplanet.Node.Services;

public sealed class Observer<T> : IObserver<T>, IDisposable
{
    private IDisposable? _subscription;

    public Observer(IObservable<T> observable)
    {
        _subscription = observable.Subscribe(this);
    }

    public System.Action? Completed { get; init; }

    public Action<Exception>? Error { get; init; }

    public Action<T>? Next { get; init; }

    public void Dispose()
    {
        if (_subscription is not null)
        {
            _subscription.Dispose();
            _subscription = null;
        }
    }

    void IObserver<T>.OnCompleted() => Completed?.Invoke();

    void IObserver<T>.OnError(Exception error) => Error?.Invoke(error);

    void IObserver<T>.OnNext(T value) => Next?.Invoke(value);
}
