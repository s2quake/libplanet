using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Transports;

public interface ITransport : IDisposable
{
    AsyncDelegate<MessageEnvelope> ProcessMessageHandler { get; }

    Peer AsPeer { get; }

    DateTimeOffset? LastMessageTimestamp { get; }

    bool Running { get; }

    Protocol Protocol { get; }

    public ImmutableSortedSet<Address> AllowedSigners { get; }

    public DifferentAppProtocolVersionEncountered DifferentAppProtocolVersionEncountered { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(TimeSpan waitFor, CancellationToken cancellationToken = default);

    Task WaitForRunningAsync();

    Task<MessageEnvelope> SendMessageAsync(
        Peer peer,
        IMessage content,
        TimeSpan? timeout,
        CancellationToken cancellationToken);

    Task<IEnumerable<MessageEnvelope>> SendMessageAsync(
        Peer peer,
        IMessage content,
        TimeSpan? timeout,
        int expectedResponses,
        bool returnWhenTimeout,
        CancellationToken cancellationToken);

    void BroadcastMessage(IEnumerable<Peer> peers, IMessage content);

    Task ReplyMessageAsync(
        IMessage content,
        byte[] identity,
        CancellationToken cancellationToken);
}
