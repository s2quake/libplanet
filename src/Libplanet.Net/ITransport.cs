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

    void Send(Peer receiver, IMessage message);

    void Send(ImmutableArray<Peer> receivers, IMessage message);
}
