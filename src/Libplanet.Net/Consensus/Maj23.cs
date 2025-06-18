using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1, TypeName = "Maj23")]
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

    public VoteFlag VoteFlag => Metadata.VoteFlag;

    public bool Verify()
    {
        var message = ModelSerializer.SerializeToBytes(Metadata);
        var signature = Signature.AsSpan();
        return Validator.Verify(message, signature);
    }
}
