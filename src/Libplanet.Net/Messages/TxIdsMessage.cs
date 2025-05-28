using Libplanet.Serialization;
using Libplanet.Types.Transactions;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed partial record class TxIdsMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<TxId> Ids { get; init; } = [];

    public override MessageType Type => MessageType.TxIds;
}
