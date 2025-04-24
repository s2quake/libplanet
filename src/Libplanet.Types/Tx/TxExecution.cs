using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using static Libplanet.Types.BencodexUtility;

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

    public static TxExecution Create(IValue value)
    {
        if (value is not List list)
        {
            throw new ArgumentException(
                $"Given {nameof(value)} must be of type {typeof(List)}: {value.GetType()}",
                nameof(value));
        }

        return new TxExecution
        {
            BlockHash = new BlockHash(list[0]),
            TxId = TxId.Create(list[1]),
            InputState = new HashDigest<SHA256>(list[2]),
            OutputState = new HashDigest<SHA256>(list[3]),
            ExceptionNames = [.. ((List)list[4]).Select(item => ((Text)item).Value)],
        };
    }

    public bool Equals(TxExecution? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public IValue ToBencodex()
    {
        return new List(
            ToValue(BlockHash),
            ToValue(TxId),
            ToValue(InputState),
            ToValue(OutputState),
            ToValue(ExceptionNames, item => new Text(item)));
    }
}
