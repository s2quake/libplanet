using Libplanet.Serialization;
using Libplanet.Types.Transactions;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetTransactionMessage : MessageContent, IEquatable<GetTransactionMessage>
{
    [Property(0)]
    public ImmutableArray<TxId> TxIds { get; init; } = [];

    public override MessageType Type => MessageType.GetTxs;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
    
    public bool Equals(GetTransactionMessage? other) => ModelResolver.Equals(this, other);
}
