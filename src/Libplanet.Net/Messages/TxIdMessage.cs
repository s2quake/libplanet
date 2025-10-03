using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("TxIdMessage", Version = 1)]
internal sealed partial record class TxIdMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<TxId> Ids { get; init; } = [];
}
