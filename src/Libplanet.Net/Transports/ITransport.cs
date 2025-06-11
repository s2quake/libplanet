using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Transports;

public interface ITransport : IDisposable
{
    AsyncDelegate<Message> ProcessMessageHandler { get; }

    Peer AsPeer { get; }

    DateTimeOffset? LastMessageTimestamp { get; }

    bool Running { get; }

    ProtocolVersion AppProtocolVersion { get; }

    public IImmutableSet<PublicKey> TrustedAppProtocolVersionSigners { get; }

    public DifferentAppProtocolVersionEncountered
        DifferentAppProtocolVersionEncountered
    { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(TimeSpan waitFor, CancellationToken cancellationToken = default);

    Task WaitForRunningAsync();

    Task<Message> SendMessageAsync(
        Peer peer,
        MessageContent content,
        TimeSpan? timeout,
        CancellationToken cancellationToken);

    Task<IEnumerable<Message>> SendMessageAsync(
        Peer peer,
        MessageContent content,
        TimeSpan? timeout,
        int expectedResponses,
        bool returnWhenTimeout,
        CancellationToken cancellationToken);

    void BroadcastMessage(IEnumerable<Peer> peers, MessageContent content);

    Task ReplyMessageAsync(
        MessageContent content,
        byte[] identity,
        CancellationToken cancellationToken);
}
