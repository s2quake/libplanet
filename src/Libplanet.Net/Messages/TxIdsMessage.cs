using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "TxIdsMessage")]
internal sealed partial record class TxIdsMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<TxId> Ids { get; init; } = [];

    public override MessageType Type => MessageType.TxIds;
}
