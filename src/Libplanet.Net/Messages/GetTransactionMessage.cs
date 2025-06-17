using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "GetTransactionMessage")]
internal sealed partial record class GetTransactionMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<TxId> TxIds { get; init; } = [];
}
