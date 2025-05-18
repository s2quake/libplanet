using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

public sealed record class ProposalClaimMetadata : IEquatable<ProposalClaimMetadata>
{
    public int Height { get; init; }

    public int Round { get; init; }

    public BlockHash BlockHash { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public Address Validator { get; init; }

    public ProposalClaim Sign(PrivateKey signer)
    {
        var bytes = ModelSerializer.SerializeToBytes(this);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new ProposalClaim { Metadata = this, Signature = signature };
    }
}
