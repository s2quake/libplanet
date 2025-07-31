using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "EvidenceResponseMessage")]
internal sealed partial record class EvidenceResponseMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<EvidenceBase> Evidence { get; init; } = [];

    [Property(1)]
    public bool IsLast { get; init; }
}
