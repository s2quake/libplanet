using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "GetProtocolMessage")]
public sealed record class GetProtocolMessage : MessageBase
{
}
