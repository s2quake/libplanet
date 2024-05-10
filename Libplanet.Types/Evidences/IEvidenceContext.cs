using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidences
{
    public interface IEvidenceContext
    {
        Block Block { get; }

        ValidatorSet ValidatorSet { get; }
    }
}
