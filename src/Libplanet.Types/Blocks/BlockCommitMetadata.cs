using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockCommitMetadata : IEquatable<BlockCommitMetadata>
{
    [Property(0)]
    public BlockHash BlockHash { get; init; }

    [Property(1)]
    [NonNegative]
    public int Height { get; init; }

    [Property(2)]
    [NonNegative]
    public int Round { get; init; }

    [Property(3)]
    [NotDefault]
    [NotEmpty]
    public ImmutableArray<Vote> Votes { get; init; } = [];

    // public BlockCommit Sign(PrivateKey signer)
    // {
    //     var signature = signer.Sign(ModelSerializer.SerializeToBytes(this));
    //     return new BlockCommit
    //     {
    //         Metadata = this,
    //         Signature = [.. signature],
    //     };
    // }
}
