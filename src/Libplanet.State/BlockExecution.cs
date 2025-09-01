using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.State;

public sealed record class BlockExecution
{
    public required Block Block { get; init; }

    public required HashDigest<SHA256> SystemActionHash { get; init; }

    public required World EnterWorld { get; init; }

    public required World LeaveWorld { get; init; }

    public ImmutableArray<ActionExecution> EnterExecutions { get; init; } = [];

    public ImmutableArray<TransactionExecution> Executions { get; init; } = [];

    public ImmutableArray<ActionExecution> LeaveExecutions { get; init; } = [];

    public HashDigest<SHA256> StateRootHash => LeaveWorld.Trie.Hash;
}
