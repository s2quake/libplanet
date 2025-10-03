using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("ProtocolRequestMessage", Version = 1)]
public sealed record class ProtocolRequestMessage : MessageBase
{
}
