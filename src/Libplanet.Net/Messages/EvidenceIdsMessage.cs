using Libplanet.Serialization;
using Libplanet.Types.Evidence;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class EvidenceIdsMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<EvidenceId> Ids { get; }

    public override MessageType Type => MessageType.EvidenceIds;
}
