using Libplanet.Net.Messages;

namespace Libplanet.Net;

public interface IMessageHandler
{
    Type MessageType { get; }

    void Handle(MessageEnvelope messageEnvelope);
}
