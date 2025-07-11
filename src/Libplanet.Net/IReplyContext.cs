using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IReplyContext : IDisposable
{
    IMessage Message { get; }

    Protocol Protocol { get; }

    Peer Sender { get; }

    DateTimeOffset Timestamp { get; }

    void Reply(IMessage message);

    void Pong() => Reply(new PongMessage());
}
