using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;
using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1)]
public sealed partial record class Maj23
{
    [Property(0)]
    public required Maj23Metadata Metadata { get; init; }

    [NotDefault]
    [Property(1)]
    public required ImmutableArray<byte> Signature { get; init; }

    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Validator;

    public VoteFlag Flag => Metadata.Flag;

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return PublicKey.Verify(Metadata.Validator, bytes, Signature);
    }
}
