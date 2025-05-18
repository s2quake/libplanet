using Libplanet.Consensus;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Net.Messages;

public class ConsensusVoteSetBitsMsg : ConsensusMsg
{
    public ConsensusVoteSetBitsMsg(VoteSetBits voteSetBits)
        : base(
            voteSetBits.Validator,
            voteSetBits.Height,
            voteSetBits.Round)
    {
        VoteSetBits = voteSetBits;
        BlockHash = voteSetBits.BlockHash;
    }

    public ConsensusVoteSetBitsMsg(byte[][] dataframes)
        : this(voteSetBits: ModelSerializer.DeserializeFromBytes<VoteSetBits>(dataframes[0]))
    {
    }

    public VoteSetBits VoteSetBits { get; }

    public BlockHash BlockHash { get; }

    public override IEnumerable<byte[]> DataFrames =>
        new List<byte[]> { ModelSerializer.SerializeToBytes(VoteSetBits) };

    public override MessageType Type => MessageType.ConsensusVoteSetBitsMsg;

    public override bool Equals(ConsensusMsg? other)
    {
        return other is ConsensusVoteSetBitsMsg message &&
               message.VoteSetBits.Equals(VoteSetBits);
    }

    public override bool Equals(object? obj)
    {
        return obj is ConsensusVoteSetBitsMsg other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, VoteSetBits);
    }
}
