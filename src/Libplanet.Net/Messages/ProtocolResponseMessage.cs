using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("ProtocolResponseMessage", Version = 1)]
public sealed record class ProtocolResponseMessage : MessageBase
{
    [Property(0)]
    public Protocol Protocol { get; init; } = Protocol.Empty;
}
