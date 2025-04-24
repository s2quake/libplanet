using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public sealed record class TxExecution
{
    internal static readonly Text FailKey = new("fail");

    internal static readonly Text InputStateKey = new("i");

    internal static readonly Text OutputStateKey = new("o");

    internal static readonly Text ExceptionNamesKey = new("e");

    public static TxExecution Create(BlockHash blockHash, TxId txId, IValue value)
    {
        if (value is not Dictionary dictionary)
        {
            throw new ArgumentException(
                $"Given {nameof(value)} must be of type " +
                $"{typeof(Bencodex.Types.Dictionary)}: {value.GetType()}",
                nameof(value));
        }

        throw new NotImplementedException();
        // if (!dictionary.TryGetValue(FailKey, out IValue fail))
        // {
        //     throw new ArgumentException(
        //         $"Given {nameof(value)} is missing fail value",
        //         nameof(value));
        // }
        // else if (fail is not Bencodex.Types.Boolean failBoolean)
        // {
        //     throw new ArgumentException(
        //         $"Given {nameof(value)} has an invalid fail value: {fail}",
        //         nameof(value));
        // }
        // else
        // {
        //     Fail = failBoolean.Value;
        // }

        // if (dictionary.TryGetValue(InputStateKey, out IValue input) &&
        //     input is Binary inputBinary)
        // {
        //     InputState = new HashDigest<SHA256>(inputBinary.ByteArray);
        // }
        // else
        // {
        //     InputState = null;
        // }

        // if (dictionary.TryGetValue(OutputStateKey, out IValue output) &&
        //     output is Binary outputBinary)
        // {
        //     OutputState = new HashDigest<SHA256>(outputBinary.ByteArray);
        // }
        // else
        // {
        //     OutputState = null;
        // }

        // if (dictionary.TryGetValue(ExceptionNamesKey, out IValue exceptions) &&
        //     exceptions is List exceptionsList)
        // {
        //     ExceptionNames = exceptionsList
        //         .Select(value => value is Text t
        //             ? (string?)t.Value
        //             : value is Null
        //                 ? (string?)null
        //                 : throw new ArgumentException(
        //                     $"Expected either {nameof(Text)} or {nameof(Null)} " +
        //                     $"but got {value.GetType()}"))
        //         .ToList();
        // }
        // else
        // {
        //     ExceptionNames = null;
        // }
    }

    public BlockHash BlockHash { get; init; }

    public TxId TxId { get; init; }

    public bool Fail => ExceptionNames.Length > 0;

    public HashDigest<SHA256> InputState { get; init; }

    public HashDigest<SHA256> OutputState { get; init; }

    public ImmutableArray<string> ExceptionNames { get; init; } = [];

    public IValue ToBencodex()
    {
        Dictionary dict = Dictionary.Empty
            .Add(FailKey, Fail);

        if (InputState is { } inputState)
        {
            dict = dict.Add(InputStateKey, inputState.Bencoded);
        }

        if (OutputState is { } outputState)
        {
            dict = dict.Add(OutputStateKey, outputState.Bencoded);
        }

        if (ExceptionNames is { } exceptionNames)
        {
            dict = dict.Add(
                ExceptionNamesKey,
                new List(exceptionNames
                    .Select(exceptionName => exceptionName is { } name
                        ? (IValue)new Text(exceptionName)
                        : (IValue)Null.Value)));
        }

        return dict;
    }
}
