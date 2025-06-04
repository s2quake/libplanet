using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "BlockExecution")]
public sealed partial record class BlockHeader
{
    public const int CurrentProtocolVersion = 0;

    [Property(0)]
    public int Version { get; init; } = CurrentProtocolVersion;

    [Property(1)]
    public int Height { get; init; }

    [Property(2)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    public Address Proposer { get; init; }

    [Property(4)]
    public BlockHash PreviousHash { get; init; }

    [Property(5)]
    public BlockCommit PreviousCommit { get; init; } = BlockCommit.Empty;

    [Property(6)]
    public HashDigest<SHA256> PreviousStateRootHash { get; init; }
}
