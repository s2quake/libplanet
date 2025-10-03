using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("EvidenceIdMessage", Version = 1)]
internal sealed partial record class EvidenceIdMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<EvidenceId> Ids { get; init; } = [];
}
