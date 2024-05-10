using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidences
{
    public sealed class EvidenceContext : IEvidenceContext
    {
        public EvidenceContext(Block block, ValidatorSet validatorSet)
        {
            Block = block;
            ValidatorSet = validatorSet;
        }

        public Block Block { get; }

        public ValidatorSet ValidatorSet { get; }
    }
}
