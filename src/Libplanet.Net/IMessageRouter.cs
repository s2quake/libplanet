using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IMessageRouter
{
    IObservable<(IMessageHandler MessageHandler, Exception Exception)> ErrorOccurred { get; }

    IObservable<MessageEnvelope> InvalidProtocol { get; }

    IDisposable Register(IMessageHandler handler);

    IDisposable RegisterMany(ImmutableArray<IMessageHandler> handlers);
}
