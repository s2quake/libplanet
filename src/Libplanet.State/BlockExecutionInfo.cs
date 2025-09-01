using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.State;

public sealed record class BlockExecutionInfo
{
    public required Block Block { get; init; }

    public required World EnterWorld { get; init; }

    public required World LeaveWorld { get; init; }

    public ImmutableArray<ActionExecutionInfo> EnterExecutions { get; init; } = [];

    public ImmutableArray<TransactionExecutionInfo> Executions { get; init; } = [];

    public ImmutableArray<ActionExecutionInfo> LeaveExecutions { get; init; } = [];

    public HashDigest<SHA256> StateRootHash => LeaveWorld.Trie.Hash;
}
