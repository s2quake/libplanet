using System.Runtime.CompilerServices;
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

    Task<MessageEnvelope> SendMessageAsync(Peer peer, IMessage message, CancellationToken cancellationToken);

    IAsyncEnumerable<IMessage> SendMessageAsStreamAsync(
        Peer peer, IMessage message, CancellationToken cancellationToken);

    void BroadcastMessage(IEnumerable<Peer> peers, IMessage message);

    void ReplyMessage(Guid identity, IMessage message);
}
