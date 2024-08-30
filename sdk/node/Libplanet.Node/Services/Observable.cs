namespace Libplanet.Node.Services;

public sealed class Observable<T> : IObservable<T>, IDisposable
{
    private readonly List<IObserver<T>> _observerList = [];
    private bool _isDisposed;

    public void Dispose()
    {
        if (!_isDisposed)
        {
            foreach (var observer in _observerList)
            {
                observer.OnCompleted();
            }

            _observerList.Clear();
            _isDisposed = true;
        }

        GC.SuppressFinalize(this);
    }

    public void Invoke(T value)
    {
        _observerList.ForEach(observer => observer.OnNext(value));
    }

    public void InvokeError(Exception exception)
    {
        _observerList.ForEach(observer => observer.OnError(exception));
    }

    IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
    {
        _observerList.Add(observer);
        return new Unsubscriber(_observerList, observer);
    }

    private sealed class Unsubscriber(List<IObserver<T>> observerList, IObserver<T> observer)
        : IDisposable
    {
        private readonly IObserver<T> _observer = observer;
        private readonly List<IObserver<T>> _observerList = observerList;

        public void Dispose()
        {
            _observerList.Remove(_observer);
        }
    }
}
