using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "ConsensusProposalClaimMessage")]
public sealed record class ConsensusProposalClaimMessage : ConsensusMessage
{
    [Property(0)]
    public required ProposalClaim ProposalClaim { get; init; }

    public override Address Validator => ProposalClaim.Validator;

    public override int Height => ProposalClaim.Height;

    public override int Round => ProposalClaim.Round;
}
