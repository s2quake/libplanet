using Libplanet.Serialization;
using Libplanet.Types.Evidence;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetEvidenceMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<EvidenceId> EvidenceIds { get; init; } = [];

    public override MessageType Type => MessageType.GetEvidence;
}
