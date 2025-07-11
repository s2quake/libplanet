using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IReplyContext
{
    IMessage Message { get; }

    Protocol Protocol { get; }

    Peer Sender { get; }

    DateTimeOffset Timestamp { get; }

    void Next(IMessage message);

    void Complete(IMessage message);

    void Pong() => Complete(new PongMessage());
}
