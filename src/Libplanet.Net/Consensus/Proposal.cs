using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1)]
public sealed partial record class Proposal
{
    [Property(0)]
    public required ProposalMetadata Metadata { get; init; }

    [Property(1)]
    [NotDefault]
    public required ImmutableArray<byte> Signature { get; init; }

    public int Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public Address Validator => Metadata.Proposer;

    public byte[] MarshaledBlock => throw new NotImplementedException();

    public int ValidRound => Metadata.ValidRound;

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return PublicKey.Verify(Metadata.Proposer, bytes, Signature);
    }
}
