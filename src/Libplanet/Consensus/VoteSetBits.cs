using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

public sealed record class VoteSetBits : IEquatable<VoteSetBits>
{
    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Validator;

    public VoteFlag Flag => Metadata.Flag;

    public ImmutableArray<bool> VoteBits => Metadata.VoteBits;

    public required VoteSetBitsMetadata Metadata { get; init; }

    public required ImmutableArray<byte> Signature { get; init; }

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return PublicKey.Verify(Metadata.Validator, bytes, Signature);
    }
}
