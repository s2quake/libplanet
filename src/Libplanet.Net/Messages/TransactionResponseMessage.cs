using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("TransactionResponseMessage", Version = 1)]
internal sealed partial record class TransactionResponseMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<Transaction> Transactions { get; init; } = [];

    [Property(1)]
    public bool IsLast { get; init; }
}
