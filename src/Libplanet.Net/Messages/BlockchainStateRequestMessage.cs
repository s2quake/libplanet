using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("BlockchainStateRequestMessage", Version = 1)]
internal sealed record class BlockchainStateRequestMessage : MessageBase
{
}
