using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "EvidenceRequestMessage")]
internal sealed partial record class EvidenceRequestMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<EvidenceId> EvidenceIds { get; init; } = [];

    [Property(1)]
    public int ChunkSize { get; init; } = 100;
}
