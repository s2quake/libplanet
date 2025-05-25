using Libplanet.Serialization;
using Libplanet.Types.Evidence;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetEvidenceMessage : MessageContent, IEquatable<GetEvidenceMessage>
{
    [Property(0)]
    public ImmutableArray<EvidenceId> EvidenceIds { get; init; } = [];

    public override MessageType Type => MessageType.GetEvidence;

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    public bool Equals(GetEvidenceMessage? other) => ModelResolver.Equals(this, other);
}
