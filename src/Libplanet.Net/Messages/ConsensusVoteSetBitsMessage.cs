using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed partial record class ConsensusVoteSetBitsMessage : ConsensusMessage
{
    [Property(0)]
    public required VoteSetBits VoteSetBits { get; init; }

    public BlockHash BlockHash => VoteSetBits.BlockHash;

    public override MessageType Type => MessageType.ConsensusVoteSetBitsMsg;

    public override Address Validator => VoteSetBits.Validator;

    public override int Height => VoteSetBits.Height;

    public override int Round => VoteSetBits.Round;
}
