using Libplanet.Net.Consensus;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

public abstract class ConsensusVoteMsg : ConsensusMsg
{
    protected ConsensusVoteMsg(
        Address validator,
        int height,
        int round,
        BlockHash blockHash,
        VoteFlag flag)
        : base(validator, height, round)
    {
        BlockHash = blockHash;
        Flag = flag;
    }

    public BlockHash BlockHash { get; }

    public VoteFlag Flag { get; }
}
