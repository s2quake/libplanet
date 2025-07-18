using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface ITransport : IAsyncDisposable
{
    MessageHandlerCollection MessageHandlers { get; }

    Peer Peer { get; }

    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    MessageEnvelope Send(Peer receiver, IMessage message, Guid? replyTo);
}
