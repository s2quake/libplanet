using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed partial record class PingMessage : MessageContent
{
    public override MessageType Type => MessageType.Ping;
}
