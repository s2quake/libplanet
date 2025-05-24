using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

public sealed record class ConsensusProposalClaimMessage : ConsensusMessage
{
    [Property(0)]
    public required ProposalClaim ProposalClaim { get; init; }

    public override MessageType Type => MessageType.ConsensusProposalClaimMsg;

    public override Address Validator => ProposalClaim.Validator;

    public override int Height => ProposalClaim.Height;

    public override int Round => ProposalClaim.Round;
}
