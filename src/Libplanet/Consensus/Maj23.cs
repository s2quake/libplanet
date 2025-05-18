using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

[Model(Version = 1)]
public sealed record class Maj23 : IEquatable<Maj23>
{
    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Validator;

    public VoteFlag Flag => Metadata.Flag;

    public required Maj23Metadata Metadata { get; init; }

    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return PublicKey.Verify(Metadata.Validator, bytes, Signature);
    }
}
