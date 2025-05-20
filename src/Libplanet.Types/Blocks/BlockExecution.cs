using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Types.Blocks;

[Model(Version = 1)]
public sealed record class BlockExecution : IEquatable<BlockExecution>, IHasKey<BlockHash>
{
    [Property(0)]
    public BlockHash BlockHash { get; init; }

    [Property(2)]
    public HashDigest<SHA256> InputState { get; init; }

    [Property(3)]
    public HashDigest<SHA256> OutputState { get; init; }

    BlockHash IHasKey<BlockHash>.Key => BlockHash;
}
