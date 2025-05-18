using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

public sealed record class Proposal : IEquatable<Proposal>
{
    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Validator;

    public byte[] MarshaledBlock => Metadata.MarshaledBlock;

    public int ValidRound => Metadata.ValidRound;

    public required ProposalMetadata Metadata { get; init; }

    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return PublicKey.Verify(Metadata.Validator, bytes, Signature);
    }
}
