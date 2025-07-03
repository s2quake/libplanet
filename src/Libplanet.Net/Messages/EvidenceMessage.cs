using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "EvidenceMessage")]
internal sealed partial record class EvidenceMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<EvidenceBase> Evidence { get; init; } = [];
}