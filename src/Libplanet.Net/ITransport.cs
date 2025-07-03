using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface ITransport : IAsyncDisposable
{
    IObservable<MessageEnvelope> ProcessMessage { get; }

    Peer Peer { get; }

    bool IsRunning { get; }

    Protocol Protocol { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<IMessage> SendAsync(Peer peer, IMessage message, CancellationToken cancellationToken);

    void Broadcast(IEnumerable<Peer> peers, IMessage message);

    void Reply(Guid identity, IMessage message);
}
