using Libplanet.Action;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public sealed record class EvidenceOptions
{
    public long MaxEvidencePendingDuration { get; init; } = 10L;
}
