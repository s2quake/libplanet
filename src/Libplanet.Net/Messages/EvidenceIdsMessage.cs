using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed partial record class EvidenceIdsMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<EvidenceId> Ids { get; init; } = [];

    public override MessageType Type => MessageType.EvidenceIds;
}
