using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("BlockHashRequestMessage", Version = 1)]
internal sealed record class BlockHashRequestMessage : MessageBase
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }
}
