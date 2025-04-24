using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Types.Blocks;
using static Libplanet.Types.BencodexUtility;

namespace Libplanet.Types.Tx;

public sealed record class TxExecution : IEquatable<TxExecution>
{
    public BlockHash BlockHash { get; init; }

    public TxId TxId { get; init; }

    [JsonIgnore]
    public bool Fail => ExceptionNames.Length > 0;

    public HashDigest<SHA256> InputState { get; init; }

    public HashDigest<SHA256> OutputState { get; init; }

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

    public bool Equals(TxExecution? other)
    {
        if (other is { } o)
        {
            return BlockHash.Equals(o.BlockHash) &&
                   TxId.Equals(o.TxId) &&
                   InputState.Equals(o.InputState) &&
                   OutputState.Equals(o.OutputState) &&
                   ExceptionNames.SequenceEqual(o.ExceptionNames);
        }

        return base.Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(BlockHash);
        hash.Add(TxId);
        hash.Add(InputState);
        hash.Add(OutputState);
        foreach (string exceptionName in ExceptionNames)
        {
            hash.Add(exceptionName);
        }

        return hash.ToHashCode();
    }

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
