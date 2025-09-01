using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Libplanet.Serialization;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "TxExecutionInfo")]
public sealed partial record class TxExecutionInfo : IHasKey<TxId>
{
    [Property(0)]
    public TxId TxId { get; init; }

    [Property(1)]
    public BlockHash BlockHash { get; init; }

    [Property(2)]
    public HashDigest<SHA256> EnterState { get; init; }

    [Property(3)]
    public HashDigest<SHA256> LeaveState { get; init; }

    [Property(4)]
    public ImmutableArray<string> ExceptionNames { get; init; } = [];

    [JsonIgnore]
    public bool Fail => ExceptionNames.Length > 0;

    TxId IHasKey<TxId>.Key => TxId;
}
