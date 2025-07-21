using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockHashRequestMessage")]
internal sealed record class BlockHashRequestMessage : MessageBase
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }
}
