using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ProtocolMessage")]
public sealed record class ProtocolMessage : MessageBase
{
    [Property(0)]
    public Protocol Protocol { get; init; } = Protocol.Empty;
}
