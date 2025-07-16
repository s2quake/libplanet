using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IReplyContext
{
    IMessage Message { get; }

    Protocol Protocol { get; }

    Peer Sender { get; }

    DateTimeOffset Timestamp { get; }

    ValueTask NextAsync(IMessage message);

    ValueTask CompleteAsync(IMessage message);

    ValueTask PongAsync() => CompleteAsync(new PongMessage());
}
