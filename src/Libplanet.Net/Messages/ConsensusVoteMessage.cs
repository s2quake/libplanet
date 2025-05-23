using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Net.Messages;

public abstract record class ConsensusVoteMessage : ConsensusMessage
{
    public abstract BlockHash BlockHash { get; }

    public abstract VoteFlag Flag { get; }
}
