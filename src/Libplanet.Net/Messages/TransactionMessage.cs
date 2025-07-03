using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "TransactionMessage")]
internal sealed partial record class TransactionMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<Transaction> Transactions { get; init; } = [];
}
