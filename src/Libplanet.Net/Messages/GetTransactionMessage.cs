using Libplanet.Serialization;
using Libplanet.Types.Transactions;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetTransactionMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<TxId> TxIds { get; init; } = [];

    public override MessageType Type => MessageType.GetTxs;
}
