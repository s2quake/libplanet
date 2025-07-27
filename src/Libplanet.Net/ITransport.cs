using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface ITransport : IService, IAsyncDisposable
{
    MessageHandlerCollection MessageHandlers { get; }

    Peer Peer { get; }

    bool IsRunning { get; }

    Protocol Protocol { get; }

    CancellationToken StoppingToken { get; }

    MessageEnvelope Post(Peer receiver, IMessage message, Guid? replyTo);
}
