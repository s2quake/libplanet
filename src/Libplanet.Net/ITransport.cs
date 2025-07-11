using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface ITransport : IAsyncDisposable
{
    IObservable<IReplyContext> Process { get; }

    Peer Peer { get; }

    bool IsRunning { get; }

    Protocol Protocol { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<IMessage> SendAsync(Peer receiver, IMessage message, CancellationToken cancellationToken);

    void Broadcast(ImmutableArray<Peer> peers, IMessage message);

    // void Reply(MessageEnvelope requestEnvelope, IMessage message);
}
