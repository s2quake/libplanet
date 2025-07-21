using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ChainStatusRequestMessage")]
internal sealed record class ChainStatusRequestMessage : MessageBase
{
}
