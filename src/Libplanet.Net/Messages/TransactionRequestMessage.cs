using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("TransactionRequestMessage", Version = 1)]
internal sealed partial record class TransactionRequestMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<TxId> TxIds { get; init; } = [];

    [Property(1)]
    public int ChunkSize { get; init; } = 100;
}
