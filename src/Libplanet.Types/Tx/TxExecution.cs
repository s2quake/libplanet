using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Common;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class TxExecution : IEquatable<TxExecution>
{
    [Property(0)]
    public BlockHash BlockHash { get; init; }

    [Property(1)]
    public TxId TxId { get; init; }

    [JsonIgnore]
    public bool Fail => ExceptionNames.Length > 0;

    [Property(2)]
    public HashDigest<SHA256> InputState { get; init; }

    [Property(3)]
    public HashDigest<SHA256> OutputState { get; init; }

    [Property(4)]
    public ImmutableArray<string> ExceptionNames { get; init; } = [];

    public bool Equals(TxExecution? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);
}
