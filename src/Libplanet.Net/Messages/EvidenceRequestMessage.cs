using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("EvidenceRequestMessage", Version = 1)]
internal sealed partial record class EvidenceRequestMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<EvidenceId> EvidenceIds { get; init; } = [];

    [Property(1)]
    public int ChunkSize { get; init; } = 100;
}
