using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ProtocolResponseMessage")]
public sealed record class ProtocolResponseMessage : MessageBase
{
    [Property(0)]
    public Protocol Protocol { get; init; } = Protocol.Empty;
}
