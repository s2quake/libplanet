using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "blkh")]
public sealed partial record class BlockHeader
{
    public const int CurrentVersion = 0;

    [Property(0)]
    [NonNegative]
    [LessThanOrEqual(CurrentVersion)]
    public int Version { get; init; } = CurrentVersion;

    [Property(1)]
    [NonNegative]
    public int Height { get; init; }

    [Property(2)]
    [NotDefault]
    public DateTimeOffset Timestamp { get; init; }

    [Property(3)]
    [NotDefault]
    public Address Proposer { get; init; }

    [Property(4)]
    public BlockHash PreviousBlockHash { get; init; }

    [Property(5)]
    public BlockCommit PreviousBlockCommit { get; init; }

    [Property(6)]
    public HashDigest<SHA256> PreviousStateRootHash { get; init; }
}
