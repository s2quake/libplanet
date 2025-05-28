using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1)]
public sealed partial record class BlockCommit : IHasKey<BlockHash>
{
    public static BlockCommit Empty { get; } = new();

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
    public ImmutableArray<Vote> Votes { get; init; } = [];

    BlockHash IHasKey<BlockHash>.Key => BlockHash;

    public HashDigest<SHA256> ToHash() => HashDigest<SHA256>.Create(ModelSerializer.SerializeToBytes(this));
}
