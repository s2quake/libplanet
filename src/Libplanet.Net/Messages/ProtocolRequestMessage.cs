using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ProtocolRequestMessage")]
public sealed record class ProtocolRequestMessage : MessageBase
{
}
