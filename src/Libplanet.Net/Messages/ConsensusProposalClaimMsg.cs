using Libplanet.Net.Consensus;
using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

public class ConsensusProposalClaimMsg : ConsensusMessage
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

    /// <inheritdoc cref="ConsensusMessage.Equals(ConsensusMessage?)"/>
    public override bool Equals(ConsensusMessage? other)
    {
        return other is ConsensusProposalClaimMsg message &&
               message.ProposalClaim.Equals(ProposalClaim);
    }

    /// <inheritdoc cref="ConsensusMessage.Equals(object?)"/>
    public override bool Equals(object? obj)
    {
        return obj is ConsensusMessage other && Equals(other);
    }

    /// <inheritdoc cref="ConsensusMessage.GetHashCode"/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Type, ProposalClaim);
    }
}
