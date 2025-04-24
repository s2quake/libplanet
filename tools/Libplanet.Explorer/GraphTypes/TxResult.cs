using System.Security.Cryptography;
using Libplanet.Common;

namespace Libplanet.Explorer.GraphTypes;

public sealed record class TxResult
{
    public TxStatus TxStatus { get; init; }

    public long BlockHeight { get; init; } = -1;

    public string BlockHash { get; init; } = string.Empty;

    public HashDigest<SHA256> InputState { get; init; }

    public HashDigest<SHA256> OutputState { get; init; }

    public ImmutableArray<string> ExceptionNames { get; init; }
}
