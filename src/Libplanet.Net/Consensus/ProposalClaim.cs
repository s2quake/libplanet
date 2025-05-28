using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Consensus;

[Model(Version = 1)]
public sealed partial record class ProposalClaim
{
    [Property(0)]
    public required ProposalClaimMetadata Metadata { get; init; }

    [Property(1)]
    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Validator;

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return PublicKey.Verify(Metadata.Validator, bytes, Signature);
    }
}
