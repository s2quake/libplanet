using Libplanet.Consensus;
using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

public class ConsensusProposalClaimMsg : ConsensusMsg
{
    public ConsensusProposalClaimMsg(ProposalClaim proposalClaim)
        : base(proposalClaim.Validator, proposalClaim.Height, proposalClaim.Round)
    {
        ProposalClaim = proposalClaim;
    }

    public ConsensusProposalClaimMsg(byte[][] dataframes)
        : this(ModelSerializer.DeserializeFromBytes<ProposalClaim>(dataframes[0]))
    {
    }

    /// <summary>
    /// A <see cref="ProposalClaim"/> of the message.
    /// </summary>
    public ProposalClaim ProposalClaim { get; }

    /// <inheritdoc cref="MessageContent.DataFrames"/>
    public override IEnumerable<byte[]> DataFrames =>
        new List<byte[]> { ModelSerializer.SerializeToBytes(ProposalClaim) };

    /// <inheritdoc cref="MessageContent.MessageType"/>
    public override MessageType Type => MessageType.ConsensusProposalClaimMsg;

    /// <inheritdoc cref="ConsensusMsg.Equals(ConsensusMsg?)"/>
    public override bool Equals(ConsensusMsg? other)
    {
        return other is ConsensusProposalClaimMsg message &&
               message.ProposalClaim.Equals(ProposalClaim);
    }

    /// <inheritdoc cref="ConsensusMsg.Equals(object?)"/>
    public override bool Equals(object? obj)
    {
        return obj is ConsensusMsg other && Equals(other);
    }

    /// <inheritdoc cref="ConsensusMsg.GetHashCode"/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Type, ProposalClaim);
    }
}
