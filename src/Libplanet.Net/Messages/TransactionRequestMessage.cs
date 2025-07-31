using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "TransactionRequestMessage")]
internal sealed partial record class TransactionRequestMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<TxId> TxIds { get; init; } = [];

    [Property(1)]
    public int ChunkSize { get; init; } = 100;
}
