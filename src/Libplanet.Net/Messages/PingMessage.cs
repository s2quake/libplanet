using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "PingMessage")]
public sealed record class PingMessage : MessageContent
{
    public override MessageType Type => MessageType.Ping;
}
