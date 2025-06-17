using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "GetEvidenceMessage")]
internal sealed partial record class GetEvidenceMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<EvidenceId> EvidenceIds { get; init; } = [];
}
