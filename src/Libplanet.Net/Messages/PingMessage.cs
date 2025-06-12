using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "PingMessage")]
public sealed record class PingMessage : MessageBase
{
    public override MessageType Type => MessageType.Ping;
}
