using System.Threading;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface ITransport : IService, IAsyncDisposable
{
    IMessageRouter MessageRouter { get; }

    Peer Peer { get; }

    bool IsRunning { get; }

    Protocol Protocol { get; }

    CancellationToken StoppingToken { get; }

    MessageEnvelope Post(Peer receiver, IMessage message, Guid? replyTo);
}
