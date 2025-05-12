using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

[Model(Version = 1)]
public sealed record class ChainDigest : IEquatable<ChainDigest>
{
    [Property(0)]
    public required Guid Id { get; init; }

    [Property(1)]
    public required BlockCommit BlockCommit { get; init; }

    [Property(2)]
    public required int Height { get; init; }
}
