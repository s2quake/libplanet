using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Transactions;

[Model(Version = 1)]
public sealed record class TxExecution : IEquatable<TxExecution>, IHasKey<TxId>
{
    [Property(0)]
    public TxId TxId { get; init; }

    [Property(1)]
    public BlockHash BlockHash { get; init; }

    [Property(2)]
    public HashDigest<SHA256> InputState { get; init; }

    [Property(3)]
    public HashDigest<SHA256> OutputState { get; init; }

    [Property(4)]
    public ImmutableArray<string> ExceptionNames { get; init; } = [];

    [JsonIgnore]
    public bool Fail => ExceptionNames.Length > 0;

    TxId IHasKey<TxId>.Key => TxId;

    public bool Equals(TxExecution? other) => ModelResolver.Equals(this, other);

    public override int GetHashCode() => ModelResolver.GetHashCode(this);
}
