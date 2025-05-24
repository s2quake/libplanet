using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

public sealed record class ConsensusProposalMessage : ConsensusMessage
{
    [Property(0)]
    public required Proposal Proposal { get; init; }

    [Property(1)]
    public required BlockHash BlockHash { get; init; }

    public override MessageType Type => MessageType.ConsensusProposal;

    public override Address Validator => Proposal.Validator;

    public override int Height => Proposal.Height;

    public override int Round => Proposal.Round;
}
