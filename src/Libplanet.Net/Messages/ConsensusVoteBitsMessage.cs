using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("ConsensusVoteBitsMessage", Version = 1)]
public sealed record class ConsensusVoteBitsMessage : ConsensusMessage
{
    [Property(0)]
    public required VoteBits VoteBits { get; init; }

    public BlockHash BlockHash => VoteBits.BlockHash;

    public override Address Validator => VoteBits.Validator;

    public override int Height => VoteBits.Height;

    public override int Round => VoteBits.Round;
}
