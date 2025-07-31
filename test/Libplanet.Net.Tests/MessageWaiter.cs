using Libplanet.Net.Messages;
using Libplanet.Net.MessageHandlers;
using System.Reactive.Subjects;

namespace Libplanet.Net.Tests;

public sealed class MessageWaiter : MessageHandlerBase<IMessage>, IDisposable
{
    private readonly Subject<IMessage> _receivedSubject = new();

    private bool _disposed;

    public IObservable<IMessage> Received => _receivedSubject;

    public void Dispose()
    {
        if (!_disposed)
        {
            _receivedSubject.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    protected override void OnHandle(IMessage message, MessageEnvelope messageEnvelope)
        => _receivedSubject.OnNext(message);
}
