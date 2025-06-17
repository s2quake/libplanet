using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Transports;

public interface ITransport : IDisposable
{
    AsyncDelegate<MessageEnvelope> ProcessMessageHandler { get; }

    Peer Peer { get; }

    bool IsRunning { get; }

    Protocol Protocol { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task<MessageEnvelope> SendMessageAsync(Peer peer, IMessage message, CancellationToken cancellationToken);

    Task<IEnumerable<MessageEnvelope>> SendMessageAsync(
        Peer peer, IMessage message, int expectedResponses, CancellationToken cancellationToken);

    void BroadcastMessage(IEnumerable<Peer> peers, IMessage message);

    Task ReplyMessageAsync(IMessage message, Guid identity, CancellationToken cancellationToken);
}
