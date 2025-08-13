using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1, TypeName = "VoteBits")]
public sealed partial record class VoteBits : IEquatable<VoteBits>
{
    [Property(0)]
    public required VoteBitsMetadata Metadata { get; init; }

    [Property(1)]
    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Validator;

    public VoteType VoteType => Metadata.VoteType;

    public ImmutableArray<bool> Bits => Metadata.Bits;

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return PublicKey.Verify(Metadata.Validator, bytes, Signature);
    }
}
