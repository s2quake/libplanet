using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockchainStateRequestMessage")]
internal sealed record class BlockchainStateRequestMessage : MessageBase
{
}
