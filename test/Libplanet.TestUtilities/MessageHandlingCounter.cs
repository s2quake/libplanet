using System.Reactive.Subjects;
using Libplanet.Net;

namespace Libplanet.TestUtilities;

public sealed class MessageHandlingCounter<T> : IDisposable
    where T : IMessage
{
    private readonly Subject<int> _countChangedSubject = new();
    private readonly IDisposable _subscription;
    private int _count;
    private bool _disposed;

    public MessageHandlingCounter(ITransport transport)
        : this(transport, _ => true)
    {
    }

    public MessageHandlingCounter(ITransport transport, Func<T, bool> predicate)
    {
        _subscription = transport.MessageRouter.Register<T>(item =>
        {
            if (predicate(item))
            {
                Interlocked.Increment(ref _count);
                _countChangedSubject.OnNext(_count);
            }
        });
    }

    public IObservable<int> CountChanged => _countChangedSubject;

    public int Count => Interlocked.CompareExchange(ref _count, 0, 0);

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
