using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "TransactionResponseMessage")]
internal sealed partial record class TransactionResponseMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<Transaction> Transactions { get; init; } = [];
}
