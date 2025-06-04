using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Serialization;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "TxExecution")]
public sealed partial record class TxExecution : IHasKey<TxId>
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
}
