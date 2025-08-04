using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IMessageHandler
{
    Type MessageType { get; }

    ValueTask HandleAsync(MessageEnvelope messageEnvelope, CancellationToken cancellationToken);
}
