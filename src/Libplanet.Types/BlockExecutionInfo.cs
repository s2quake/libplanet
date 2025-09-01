using System.Security.Cryptography;
using Libplanet.Serialization;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "BlockExecutionInfo")]
public sealed partial record class BlockExecutionResult : IEquatable<BlockExecutionResult>, IHasKey<BlockHash>
{
    [Property(0)]
    public BlockHash BlockHash { get; init; }

    [Property(1)]
    public HashDigest<SHA256> EnterState { get; init; }

    [Property(2)]
    public HashDigest<SHA256> LeaveState { get; init; }

    BlockHash IHasKey<BlockHash>.Key => BlockHash;
}
