using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed partial record class PongMessage : MessageContent
{
    public override MessageType Type => MessageType.Pong;
}
