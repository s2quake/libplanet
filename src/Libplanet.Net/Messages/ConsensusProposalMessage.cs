using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

public sealed record class ConsensusProposalMessage : ConsensusMessage
{
    [Property(0)]
    public required Proposal Proposal { get; init; }

    public BlockHash BlockHash => Proposal.BlockHash;

    public override MessageType Type => MessageType.ConsensusProposal;

    public override Address Validator => Proposal.Validator;

    public override int Height => Proposal.Height;

    public override int Round => Proposal.Round;
}
