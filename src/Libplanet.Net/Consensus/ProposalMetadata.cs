using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1, TypeName = "ProposalMetadata")]
public sealed partial record class ProposalMetadata
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }

    [Property(1)]
    [NonNegative]
    public required int Height { get; init; }

    [Property(2)]
    [NonNegative]
    public int Round { get; init; }

    [Property(3)]
    [NotDefault]
    public required DateTimeOffset Timestamp { get; init; }

    [Property(4)]
    [NotDefault]
    public required Address Proposer { get; init; }

    [Property(5)]
    [GreaterThanOrEqual(-1)]
    public int ValidRound { get; init; }

    public Proposal Sign(ISigner signer, Block block)
    {
        if (Height != block.Height)
        {
            throw new ArgumentException($"Height mismatch: {Height} != {block.Height}", nameof(block));
        }

        if (block.BlockHash != BlockHash)
        {
            throw new ArgumentException($"BlockHash mismatch: {BlockHash} != {block.BlockHash}", nameof(block));
        }

        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var bytes = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new Proposal { Metadata = this, Signature = signature, Block = block };
    }
}
